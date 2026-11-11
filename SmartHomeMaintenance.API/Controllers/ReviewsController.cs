using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using SmartHomeMaintenance.Application.Common.Interfaces;
using SmartHomeMaintenance.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;

namespace SmartHomeMaintenance.API.Controllers
{
    public class CreateReviewRequest
    {
        public string? BookingId { get; set; }
        public string? ProviderId { get; set; }
        public string Provider { get; set; } = string.Empty;
        public int Stars { get; set; }
        public string Text { get; set; } = string.Empty;
    }

    public class UpdateCustomerReviewRequest
    {
        public int Stars { get; set; }
        public string Text { get; set; } = string.Empty;
    }

    public class ReplyReviewRequest
    {
        public string Reply { get; set; } = string.Empty;
    }

    public class UpdateReviewStatusRequest
    {
        public string Status { get; set; } = "Published";
        public string? RemovedAt { get; set; }
    }

    public class ReviewDto
    {
        public string Id { get; set; } = string.Empty;
        public string? BookingId { get; set; }
        public string? ProviderId { get; set; }
        public string Customer { get; set; } = string.Empty;
        public string Provider { get; set; } = string.Empty;
        public int Stars { get; set; }
        public string Text { get; set; } = string.Empty;
        public string Status { get; set; } = "Published";
        public string Date { get; set; } = string.Empty;
        public string? RemovedAt { get; set; }
        public string? Reply { get; set; }
        public string? ReplyAt { get; set; }
        public string? UpdatedAt { get; set; }
        public bool CanEdit { get; set; }
    }

    [Authorize]
    [ApiController]
    [Route("api/[controller]")]
    public class ReviewsController : ControllerBase
    {
        private readonly IApplicationDbContext _context;

        public ReviewsController(IApplicationDbContext context)
        {
            _context = context;
        }

        private int? GetCurrentUserId()
        {
            var idStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            return int.TryParse(idStr, out var id) ? id : null;
        }

        private static ReviewDto ToDto(Review r) => new ReviewDto
        {
            Id = r.ReviewCode,
            BookingId = r.BookingCode,
            ProviderId = r.ProviderId.ToString(),
            Customer = r.CustomerName,
            Provider = r.ProviderName,
            Stars = r.Stars,
            Text = r.Comment,
            Status = r.Status,
            Date = r.CreatedAt.ToString("o"),
            RemovedAt = r.RemovedAt,
            Reply = r.Reply,
            ReplyAt = r.ReplyAt?.ToString("o"),
            UpdatedAt = r.UpdatedAt?.ToString("o"),
            CanEdit = (DateTime.UtcNow - r.CreatedAt) <= TimeSpan.FromHours(24)
        };

        private Task<Review?> FindReviewAsync(string code)
        {
            var trimmed = (code ?? string.Empty).Trim();
            if (int.TryParse(trimmed, out var numericId))
            {
                return _context.Reviews.FirstOrDefaultAsync(r => r.ReviewCode == trimmed || r.Id == numericId);
            }
            return _context.Reviews.FirstOrDefaultAsync(r => r.ReviewCode == trimmed);
        }

        private async Task RecalculateProviderAvgRatingAsync(int providerId)
        {
            var sp = await _context.ServiceProviders.FirstOrDefaultAsync(s => s.ProviderId == providerId);
            if (sp == null) return;

            var publishedRatings = await _context.Reviews
                .Where(r => r.ProviderId == providerId && r.Status == "Published")
                .Select(r => (decimal)r.Stars)
                .ToListAsync();

            sp.AvgRating = publishedRatings.Count > 0
                ? Math.Round(publishedRatings.Average(), 2)
                : 0.00m;

            await _context.SaveChangesAsync();
        }

        // ---------------- PUBLIC / PROVIDER ENDPOINTS ----------------

        [AllowAnonymous]
        [HttpGet("provider/{providerId:int}")]
        public async Task<IActionResult> GetProviderReviews(int providerId)
        {
            var list = await _context.Reviews
                .Where(r => r.ProviderId == providerId && r.Status == "Published")
                .OrderByDescending(r => r.Id)
                .ToListAsync();

            return Ok(list.Select(ToDto));
        }

        [Authorize(Roles = "ServiceProvider,Admin")]
        [HttpPut("provider/{code}/reply")]
        public async Task<IActionResult> ReplyToReview(string code, [FromBody] ReplyReviewRequest request)
        {
            var review = await FindReviewAsync(code);
            if (review == null) return NotFound(new { message = "Review not found." });

            var userId = GetCurrentUserId();
            if (!User.IsInRole("Admin") && (!userId.HasValue || review.ProviderId != userId.Value))
            {
                return Forbid();
            }

            if (request == null || string.IsNullOrWhiteSpace(request.Reply))
            {
                return BadRequest(new { message = "Reply message cannot be empty." });
            }

            review.Reply = request.Reply.Trim();
            review.ReplyAt = DateTime.UtcNow;

            _context.Notifications.Add(new AppNotification
            {
                UserId = review.CustomerId,
                Title = $"{review.ProviderName} replied to your review",
                Message = $"\"{review.Reply}\"",
                Type = "info",
                LinkUrl = $"/customer/provider/{review.ProviderId}",
                CreatedAt = DateTime.UtcNow
            });

            await _context.SaveChangesAsync();
            return Ok(ToDto(review));
        }

        [Authorize(Roles = "ServiceProvider,Admin")]
        [HttpGet("provider/{providerId:int}/export-pdf")]
        public async Task<IActionResult> ExportProviderRatingsPdf(int providerId)
        {
            var userId = GetCurrentUserId();
            if (!User.IsInRole("Admin") && (!userId.HasValue || userId.Value != providerId))
            {
                return Forbid();
            }

            var sp = await _context.ServiceProviders
                .Include(s => s.User)
                .FirstOrDefaultAsync(s => s.ProviderId == providerId);

            if (sp == null) return NotFound(new { message = "Service provider not found." });

            var reviews = await _context.Reviews
                .Where(r => r.ProviderId == providerId && r.Status == "Published")
                .OrderByDescending(r => r.CreatedAt)
                .ToListAsync();

            var avg = reviews.Count > 0 ? Math.Round(reviews.Average(r => r.Stars), 1) : (double)sp.AvgRating;

            var pdfBytes = Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(32);
                    page.DefaultTextStyle(x => x.FontSize(10).FontColor(Colors.Grey.Darken4));

                    page.Header().Column(col =>
                    {
                        col.Item().Text("Smart Home Maintenance Services (SHMS)")
                            .FontSize(16).Bold().FontColor(Colors.Blue.Darken3);
                        col.Item().Text($"Ratings & Remarks Report — {sp.User.FullName}")
                            .FontSize(12).SemiBold().FontColor(Colors.Grey.Darken2);
                        col.Item().Text($"Generated: {DateTime.UtcNow:yyyy-MM-dd HH:mm} UTC  |  Average Rating: {avg:0.0}/5.0  |  Total Reviews: {reviews.Count}")
                            .FontSize(9).FontColor(Colors.Grey.Medium);
                        col.Item().PaddingTop(8).LineHorizontal(1).LineColor(Colors.Grey.Lighten2);
                    });

                    page.Content().PaddingVertical(12).Column(col =>
                    {
                        if (reviews.Count == 0)
                        {
                            col.Item().Text("No customer ratings recorded yet.").Italic().FontColor(Colors.Grey.Medium);
                            return;
                        }

                        col.Item().Table(table =>
                        {
                            table.ColumnsDefinition(columns =>
                            {
                                columns.ConstantColumn(65);
                                columns.RelativeColumn(1.4f);
                                columns.ConstantColumn(55);
                                columns.RelativeColumn(2.6f);
                                columns.ConstantColumn(75);
                            });

                            table.Header(header =>
                            {
                                header.Cell().Background(Colors.Blue.Darken3).Padding(6).Text("Code").Bold().FontColor(Colors.White);
                                header.Cell().Background(Colors.Blue.Darken3).Padding(6).Text("Customer").Bold().FontColor(Colors.White);
                                header.Cell().Background(Colors.Blue.Darken3).Padding(6).Text("Stars").Bold().FontColor(Colors.White);
                                header.Cell().Background(Colors.Blue.Darken3).Padding(6).Text("Customer Remarks & Reply").Bold().FontColor(Colors.White);
                                header.Cell().Background(Colors.Blue.Darken3).Padding(6).Text("Date").Bold().FontColor(Colors.White);
                            });

                            foreach (var r in reviews)
                            {
                                table.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(6).Text(r.ReviewCode);
                                table.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(6).Text(r.CustomerName);
                                table.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(6).Text($"{r.Stars} / 5");
                                table.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(6).Column(c =>
                                {
                                    c.Item().Text(r.Comment);
                                    if (!string.IsNullOrWhiteSpace(r.Reply))
                                    {
                                        c.Item().PaddingTop(3).Text($"Reply: {r.Reply}").FontSize(9).Italic().FontColor(Colors.Blue.Darken2);
                                    }
                                });
                                table.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(6).Text(r.CreatedAt.ToString("yyyy-MM-dd"));
                            }
                        });
                    });

                    page.Footer().AlignCenter().Text(x =>
                    {
                        x.Span("SHMS Ratings Export • Page ");
                        x.CurrentPageNumber();
                    });
                });
            }).GeneratePdf();

            var fileName = $"SHMS_Ratings_{sp.User.FullName.Replace(" ", "_")}_{DateTime.UtcNow:yyyyMMdd}.pdf";
            return File(pdfBytes, "application/pdf", fileName);
        }

        // ---------------- CUSTOMER ENDPOINTS ----------------

        [Authorize(Roles = "Customer,Admin")]
        [HttpGet("customer/my")]
        public async Task<IActionResult> GetMyReviews()
        {
            var userId = GetCurrentUserId();
            if (!userId.HasValue) return Unauthorized();

            var list = await _context.Reviews
                .Where(r => r.CustomerId == userId.Value && r.Status != "Removed")
                .OrderByDescending(r => r.Id)
                .ToListAsync();

            return Ok(list.Select(ToDto));
        }

        [Authorize(Roles = "Customer")]
        [HttpPost("customer")]
        public async Task<IActionResult> CreateReview([FromBody] CreateReviewRequest request)
        {
            var userId = GetCurrentUserId();
            if (!userId.HasValue) return Unauthorized();

            if (request == null || request.Stars < 1 || request.Stars > 5)
            {
                return BadRequest(new { message = "Please select a rating between 1 and 5 stars." });
            }

            if (request.Text != null && request.Text.Length > 500)
            {
                return BadRequest(new { message = "Remarks cannot exceed 500 characters." });
            }

            var customerUser = await _context.Users
                .Include(u => u.Customer)
                .FirstOrDefaultAsync(u => u.Id == userId.Value);

            if (customerUser == null || customerUser.Customer == null)
            {
                return NotFound(new { message = "Customer profile not found." });
            }

            if (!string.IsNullOrWhiteSpace(request.BookingId))
            {
                var bookingCode = request.BookingId.Trim();
                var booking = await _context.Bookings.FirstOrDefaultAsync(b => b.BookingCode == bookingCode && b.CustomerId == userId.Value);
                if (booking != null && booking.Status != "Completed")
                {
                    return BadRequest(new { message = "Cannot rate before service completion." });
                }

                var alreadyRated = await _context.Reviews.AnyAsync(r =>
                    r.CustomerId == userId.Value &&
                    r.BookingCode == bookingCode &&
                    r.Status != "Removed");

                if (alreadyRated)
                {
                    return BadRequest(new { message = "You have already rated this booking." });
                }
            }

            int providerId = 0;
            if (!string.IsNullOrWhiteSpace(request.ProviderId) && int.TryParse(request.ProviderId, out var parsedSpId))
            {
                providerId = parsedSpId;
            }
            else if (!string.IsNullOrWhiteSpace(request.BookingId))
            {
                var b = await _context.Bookings.FirstOrDefaultAsync(bk => bk.BookingCode == request.BookingId.Trim());
                if (b != null) providerId = b.ProviderId;
            }

            SmartHomeMaintenance.Domain.Entities.ServiceProvider? spEntity = null;
            if (providerId > 0)
            {
                spEntity = await _context.ServiceProviders
                    .Include(s => s.User)
                    .FirstOrDefaultAsync(s => s.ProviderId == providerId);
            }
            else if (!string.IsNullOrWhiteSpace(request.Provider))
            {
                var pName = request.Provider.Trim().ToLower();
                spEntity = await _context.ServiceProviders
                    .Include(s => s.User)
                    .FirstOrDefaultAsync(s => s.User.FullName.ToLower() == pName);
            }

            if (spEntity == null || spEntity.User == null)
            {
                return NotFound(new { message = "Service provider not found." });
            }

            string code;
            do
            {
                code = $"REV-{Random.Shared.Next(1000, 10000)}";
            } while (await _context.Reviews.AnyAsync(r => r.ReviewCode == code));

            var review = new Review
            {
                ReviewCode = code,
                BookingCode = string.IsNullOrWhiteSpace(request.BookingId) ? null : request.BookingId.Trim(),
                CustomerId = userId.Value,
                CustomerName = customerUser.FullName,
                ProviderId = spEntity.ProviderId,
                ProviderName = spEntity.User.FullName,
                Stars = request.Stars,
                Comment = string.IsNullOrWhiteSpace(request.Text) ? "Great service!" : request.Text.Trim(),
                Status = "Published",
                CreatedAt = DateTime.UtcNow
            };

            _context.Reviews.Add(review);

            _context.Notifications.Add(new AppNotification
            {
                UserId = spEntity.ProviderId,
                Title = $"New {review.Stars}-Star Rating Received",
                Message = $"{customerUser.FullName} rated your work {review.Stars}/5: \"{review.Comment}\"",
                Type = "success",
                LinkUrl = "/sp/ratings",
                CreatedAt = DateTime.UtcNow
            });

            await _context.SaveChangesAsync();

            await RecalculateProviderAvgRatingAsync(spEntity.ProviderId);

            return Ok(ToDto(review));
        }

        // FR-CUST-65: Customer can edit their rating & remarks within 24 hours of submission
        [Authorize(Roles = "Customer")]
        [HttpPut("customer/{code}")]
        public async Task<IActionResult> UpdateCustomerReview(string code, [FromBody] UpdateCustomerReviewRequest request)
        {
            var userId = GetCurrentUserId();
            if (!userId.HasValue) return Unauthorized();

            var review = await FindReviewAsync(code);
            if (review == null) return NotFound(new { message = "Review not found." });
            if (review.CustomerId != userId.Value) return Forbid();

            if ((DateTime.UtcNow - review.CreatedAt) > TimeSpan.FromHours(24))
            {
                return BadRequest(new { message = "Ratings can only be edited within 24 hours of submission." });
            }

            if (request == null || request.Stars < 1 || request.Stars > 5)
            {
                return BadRequest(new { message = "Please select a rating between 1 and 5 stars." });
            }

            if (request.Text != null && request.Text.Length > 500)
            {
                return BadRequest(new { message = "Remarks cannot exceed 500 characters." });
            }

            review.Stars = request.Stars;
            review.Comment = string.IsNullOrWhiteSpace(request.Text) ? review.Comment : request.Text.Trim();
            review.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();
            await RecalculateProviderAvgRatingAsync(review.ProviderId);

            return Ok(ToDto(review));
        }

        // ---------------- ADMIN ENDPOINTS ----------------

        [Authorize(Roles = "Admin")]
        [HttpGet("admin/all")]
        public async Task<IActionResult> GetAllForAdmin()
        {
            var list = await _context.Reviews
                .OrderByDescending(r => r.Id)
                .ToListAsync();

            return Ok(list.Select(ToDto));
        }

        [Authorize(Roles = "Admin")]
        [HttpPut("admin/{code}/status")]
        public async Task<IActionResult> UpdateStatus(string code, [FromBody] UpdateReviewStatusRequest request)
        {
            var review = await FindReviewAsync(code);
            if (review == null) return NotFound(new { message = "Review not found." });

            review.Status = string.IsNullOrWhiteSpace(request?.Status) ? review.Status : request.Status.Trim();
            review.RemovedAt = request?.RemovedAt;
            await _context.SaveChangesAsync();

            await RecalculateProviderAvgRatingAsync(review.ProviderId);
            return Ok(ToDto(review));
        }

        [Authorize(Roles = "Admin")]
        [HttpDelete("admin/{code}")]
        public async Task<IActionResult> DeleteReview(string code)
        {
            var review = await FindReviewAsync(code);
            if (review == null) return NotFound(new { message = "Review not found." });

            var spId = review.ProviderId;
            _context.Reviews.Remove(review);
            await _context.SaveChangesAsync();

            await RecalculateProviderAvgRatingAsync(spId);
            return Ok(new { success = true });
        }
    }
}
