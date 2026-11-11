using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmartHomeMaintenance.Application.Common.Interfaces;
using SmartHomeMaintenance.Domain.Entities;
using SmartHomeMaintenance.Domain.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;

namespace SmartHomeMaintenance.API.Controllers
{
    public class CreateComplaintRequest
    {
        public string BookingId { get; set; } = string.Empty;
        public string? ProviderId { get; set; }
        public string ProviderName { get; set; } = string.Empty;
        public string? Subject { get; set; }
        public string Reason { get; set; } = string.Empty;
        public string? Severity { get; set; }
        public string? EvidenceLink { get; set; }
    }

    public class UpdateComplaintRequest
    {
        public string Status { get; set; } = "Pending";
        public string? Notes { get; set; }
        public string? DismissReason { get; set; }
        public string? Severity { get; set; }
    }

    public class WarnProviderRequest
    {
        public string? WarningMessage { get; set; }
    }

    public class BlockProviderFromComplaintRequest
    {
        public string? Reason { get; set; }
    }

    public class ComplaintDto
    {
        public string Id { get; set; } = string.Empty;
        public int CustomerId { get; set; }
        public string Customer { get; set; } = string.Empty;
        public string ProviderId { get; set; } = string.Empty;
        public string Provider { get; set; } = string.Empty;
        public string Subject { get; set; } = string.Empty;
        public string Reason { get; set; } = string.Empty;
        public string Filed { get; set; } = string.Empty;
        public string Status { get; set; } = "Pending";
        public string Severity { get; set; } = "Normal";
        public string? Notes { get; set; }
        public string? DismissReason { get; set; }
        public string? BookingId { get; set; }
        public string? EvidenceLink { get; set; }
        public int WarningsIssued { get; set; }
    }

    [Authorize]
    [ApiController]
    [Route("api/[controller]")]
    public class ComplaintsController : ControllerBase
    {
        private readonly IApplicationDbContext _context;
        private readonly IEmailService _emailService;

        public ComplaintsController(IApplicationDbContext context, IEmailService emailService)
        {
            _context = context;
            _emailService = emailService;
        }

        private int? GetCurrentUserId()
        {
            var idStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            return int.TryParse(idStr, out var id) ? id : null;
        }

        private static ComplaintDto ToDto(Complaint c) => new ComplaintDto
        {
            Id = c.ComplaintCode,
            CustomerId = c.CustomerId,
            Customer = c.CustomerName,
            ProviderId = c.ProviderId.ToString(),
            Provider = c.ProviderName,
            Subject = c.Subject,
            Reason = c.Description,
            Filed = c.FiledAt,
            Status = c.Status,
            Severity = c.Severity,
            Notes = c.AdminNotes,
            DismissReason = c.DismissReason,
            BookingId = c.BookingCode,
            EvidenceLink = c.EvidenceUrl,
            WarningsIssued = c.WarningsIssued
        };

        private Task<Complaint?> FindComplaintAsync(string code)
        {
            var trimmed = (code ?? string.Empty).Trim();
            if (int.TryParse(trimmed, out var numericId))
            {
                return _context.Complaints.FirstOrDefaultAsync(c => c.ComplaintCode == trimmed || c.Id == numericId);
            }
            return _context.Complaints.FirstOrDefaultAsync(c => c.ComplaintCode == trimmed);
        }

        [Authorize(Roles = "Customer,Admin")]
        [HttpGet("customer/my")]
        public async Task<IActionResult> GetMyComplaints()
        {
            var userId = GetCurrentUserId();
            if (!userId.HasValue) return Unauthorized();

            var list = await _context.Complaints
                .Where(c => c.CustomerId == userId.Value)
                .OrderByDescending(c => c.Id)
                .ToListAsync();

            return Ok(list.Select(ToDto));
        }

        [Authorize(Roles = "Customer,Admin")]
        [HttpGet("{code}")]
        public async Task<IActionResult> GetComplaintById(string code)
        {
            var c = await FindComplaintAsync(code);
            if (c == null) return NotFound(new { message = "Complaint not found." });

            var userId = GetCurrentUserId();
            if (!User.IsInRole("Admin") && (!userId.HasValue || c.CustomerId != userId.Value))
            {
                return Forbid();
            }

            return Ok(ToDto(c));
        }

        [Authorize(Roles = "Customer")]
        [HttpPost("customer")]
        public async Task<IActionResult> FileComplaint([FromBody] CreateComplaintRequest request)
        {
            var userId = GetCurrentUserId();
            if (!userId.HasValue) return Unauthorized();

            if (request == null || string.IsNullOrWhiteSpace(request.BookingId))
            {
                return BadRequest(new { message = "Complaint must be linked to a booking." });
            }

            var desc = (request.Reason ?? string.Empty).Trim();
            if (desc.Length < 20)
            {
                return BadRequest(new { message = "Please provide a detailed description of at least 20 characters." });
            }

            var customerUser = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId.Value);
            if (customerUser == null) return Unauthorized();

            var bookingCode = request.BookingId.Trim();
            var booking = await _context.Bookings.FirstOrDefaultAsync(b => b.BookingCode == bookingCode && b.CustomerId == userId.Value);
            if (booking == null)
            {
                return NotFound(new { message = "Selected booking not found." });
            }

            if (booking.Status != "Completed" && booking.Status != "In Progress")
            {
                return BadRequest(new { message = "Complaints can only be filed for In Progress or Completed bookings." });
            }

            // FR-CUST-75: Do not allow multiple complaints for same booking if one is already active OR resolved/dismissed without new reason
            var hasExisting = await _context.Complaints.AnyAsync(c =>
                c.BookingCode == bookingCode &&
                c.CustomerId == userId.Value &&
                (c.Status == "Pending" || c.Status == "In Review" || c.Status == "Resolved"));

            if (hasExisting)
            {
                return BadRequest(new { message = "You have already filed a complaint for this booking." });
            }

            string code;
            do
            {
                code = $"CMP-{Random.Shared.Next(1000, 10000)}";
            } while (await _context.Complaints.AnyAsync(c => c.ComplaintCode == code));

            var complaint = new Complaint
            {
                ComplaintCode = code,
                BookingCode = booking.BookingCode,
                CustomerId = userId.Value,
                CustomerName = customerUser.FullName,
                ProviderId = booking.ProviderId,
                ProviderName = booking.ProviderName,
                Subject = string.IsNullOrWhiteSpace(request.Subject) ? "Unsatisfactory Service Quality" : request.Subject.Trim(),
                Description = desc,
                Status = "Pending",
                Severity = string.IsNullOrWhiteSpace(request.Severity) ? "Normal" : request.Severity.Trim(),
                EvidenceUrl = string.IsNullOrWhiteSpace(request.EvidenceLink) ? null : request.EvidenceLink.Trim(),
                FiledAt = DateTime.UtcNow.ToString("MMM d, yyyy"),
                CreatedAt = DateTime.UtcNow
            };

            _context.Complaints.Add(complaint);

            // In-app notifications for Customer, SP, and Admins
            _context.Notifications.Add(new AppNotification
            {
                UserId = userId.Value,
                Title = $"Complaint Logged ({code})",
                Message = $"Your complaint regarding {booking.ProviderName} (Booking {booking.BookingCode}) has been submitted for admin review.",
                Type = "info",
                LinkUrl = $"/customer/complaints/{code}",
                CreatedAt = DateTime.UtcNow
            });

            _context.Notifications.Add(new AppNotification
            {
                UserId = booking.ProviderId,
                Title = $"Complaint Filed ({code})",
                Message = $"A customer submitted a complaint ({complaint.Subject}) for booking {booking.BookingCode}.",
                Type = "warning",
                LinkUrl = "/sp/bookings",
                CreatedAt = DateTime.UtcNow
            });

            var admins = await _context.Users.Where(u => u.UserType == UserType.Admin && u.IsActive).ToListAsync();
            foreach (var adm in admins)
            {
                _context.Notifications.Add(new AppNotification
                {
                    UserId = adm.Id,
                    Title = $"New Customer Complaint ({code})",
                    Message = $"{customerUser.FullName} filed a complaint against {booking.ProviderName}: {complaint.Subject}.",
                    Type = "warning",
                    LinkUrl = $"/admin/complaints/{code}",
                    CreatedAt = DateTime.UtcNow
                });
            }

            await _context.SaveChangesAsync();

            // Non-blocking emails
            var spUser = await _context.Users.FirstOrDefaultAsync(u => u.Id == booking.ProviderId);
            if (!string.IsNullOrWhiteSpace(customerUser.Email))
            {
                _ = Task.Run(() => _emailService.SendNotificationEmailAsync(
                    customerUser.Email, customerUser.FullName,
                    $"Complaint Filed ({code}) - SHMS", "COMPLAINT LOGGED",
                    "Support & Resolution",
                    $"Your complaint ({code}) has been received",
                    $"We received your complaint regarding {booking.ProviderName} (Booking {booking.BookingCode}). Admin will review your complaint within 3 business days.",
                    $"Complaint ID: {code} • Status: Pending", false));
            }
            if (spUser != null && !string.IsNullOrWhiteSpace(spUser.Email))
            {
                _ = Task.Run(() => _emailService.SendNotificationEmailAsync(
                    spUser.Email, spUser.FullName,
                    $"Service Complaint Notice ({code}) - SHMS", "COMPLAINT NOTICE",
                    "Quality & Compliance",
                    $"A customer complaint was filed for booking {booking.BookingCode}",
                    $"Subject: {complaint.Subject}\nDetails: {desc}",
                    $"Complaint Reference: {code} • Status: Pending Review", true));
            }

            return Ok(ToDto(complaint));
        }

        // ---------------- ADMIN ENDPOINTS ----------------

        [Authorize(Roles = "Admin")]
        [HttpGet("admin/all")]
        public async Task<IActionResult> GetAllComplaints()
        {
            var list = await _context.Complaints
                .OrderByDescending(c => c.Id)
                .ToListAsync();

            return Ok(list.Select(ToDto));
        }

        [Authorize(Roles = "Admin")]
        [HttpPut("admin/{code}")]
        public async Task<IActionResult> UpdateComplaint(string code, [FromBody] UpdateComplaintRequest request)
        {
            var c = await FindComplaintAsync(code);
            if (c == null) return NotFound(new { message = "Complaint not found." });

            if (request.Status == "Dismissed" && string.IsNullOrWhiteSpace(request.DismissReason))
            {
                return BadRequest(new { message = "Dismissal reason is required when dismissing a complaint." });
            }

            c.Status = string.IsNullOrWhiteSpace(request.Status) ? c.Status : request.Status.Trim();
            c.AdminNotes = request.Notes;
            c.DismissReason = request.DismissReason;
            if (!string.IsNullOrWhiteSpace(request.Severity)) c.Severity = request.Severity.Trim();
            if (c.Status == "Resolved" || c.Status == "Dismissed")
            {
                c.ResolvedAt = DateTime.UtcNow;
            }

            _context.Notifications.Add(new AppNotification
            {
                UserId = c.CustomerId,
                Title = $"Complaint Update ({c.ComplaintCode})",
                Message = $"Your complaint status changed to {c.Status}.{(string.IsNullOrWhiteSpace(c.AdminNotes) ? "" : " Resolution: " + c.AdminNotes)}",
                Type = c.Status == "Resolved" ? "success" : "info",
                LinkUrl = $"/customer/complaints/{c.ComplaintCode}",
                CreatedAt = DateTime.UtcNow
            });

            await _context.SaveChangesAsync();

            var custUser = await _context.Users.FirstOrDefaultAsync(u => u.Id == c.CustomerId);
            if (custUser != null && !string.IsNullOrWhiteSpace(custUser.Email))
            {
                var detailText = c.Status == "Dismissed"
                    ? $"Your complaint ({c.ComplaintCode}) was dismissed. Reason: {c.DismissReason}"
                    : $"Your complaint ({c.ComplaintCode}) status is now {c.Status}.{(string.IsNullOrWhiteSpace(c.AdminNotes) ? "" : "\nNotes: " + c.AdminNotes)}";

                _ = Task.Run(() => _emailService.SendNotificationEmailAsync(
                    custUser.Email, custUser.FullName,
                    $"Complaint {c.Status} ({c.ComplaintCode}) - SHMS",
                    $"COMPLAINT {c.Status.ToUpper()}", "Support Resolution Update",
                    $"Complaint {c.ComplaintCode} is now {c.Status}",
                    detailText, $"Complaint ID: {c.ComplaintCode} • Status: {c.Status}", c.Status == "Dismissed"));
            }

            return Ok(ToDto(c));
        }

        [Authorize(Roles = "Admin")]
        [HttpPost("admin/{code}/warn-sp")]
        public async Task<IActionResult> WarnProvider(string code, [FromBody] WarnProviderRequest? request)
        {
            var c = await FindComplaintAsync(code);
            if (c == null) return NotFound(new { message = "Complaint not found." });

            c.WarningsIssued += 1;
            if (c.Status == "Pending") c.Status = "In Review";
            var msg = string.IsNullOrWhiteSpace(request?.WarningMessage)
                ? $"Formal warning issued regarding complaint {c.ComplaintCode} ({c.Subject})."
                : request.WarningMessage.Trim();

            _context.Notifications.Add(new AppNotification
            {
                UserId = c.ProviderId,
                Title = "Formal Admin Warning",
                Message = msg,
                Type = "danger",
                LinkUrl = "/sp/notifications",
                CreatedAt = DateTime.UtcNow
            });

            await _context.SaveChangesAsync();

            var spUser = await _context.Users.FirstOrDefaultAsync(u => u.Id == c.ProviderId);
            if (spUser != null && !string.IsNullOrWhiteSpace(spUser.Email))
            {
                _ = Task.Run(() => _emailService.SendNotificationEmailAsync(
                    spUser.Email, spUser.FullName,
                    $"Formal Warning Issued ({c.ComplaintCode}) - SHMS",
                    "FORMAL WARNING", "Account Compliance Notice",
                    "You have received an official warning from SHMS Admin",
                    $"{msg}\nTotal warnings recorded on this complaint: {c.WarningsIssued}. Repeated policy violations may result in account suspension.",
                    $"Complaint Reference: {c.ComplaintCode}", true));
            }

            return Ok(ToDto(c));
        }

        [Authorize(Roles = "Admin")]
        [HttpPost("admin/{code}/block-sp")]
        public async Task<IActionResult> BlockProviderFromComplaint(string code, [FromBody] BlockProviderFromComplaintRequest? request)
        {
            var c = await FindComplaintAsync(code);
            if (c == null) return NotFound(new { message = "Complaint not found." });

            var spUser = await _context.Users.FirstOrDefaultAsync(u => u.Id == c.ProviderId);
            if (spUser == null) return NotFound(new { message = "Service provider account not found." });

            spUser.IsActive = false;
            c.Status = "Resolved";
            c.ResolvedAt = DateTime.UtcNow;
            var blockReason = string.IsNullOrWhiteSpace(request?.Reason) ? $"Blocked due to complaint {c.ComplaintCode}" : request.Reason.Trim();
            c.AdminNotes = string.IsNullOrWhiteSpace(c.AdminNotes) ? blockReason : $"{c.AdminNotes} | {blockReason}";

            _context.Notifications.Add(new AppNotification
            {
                UserId = c.CustomerId,
                Title = $"Complaint Resolved ({c.ComplaintCode})",
                Message = $"Action taken: Provider {c.ProviderName} has been blocked and your complaint is resolved.",
                Type = "success",
                LinkUrl = $"/customer/complaints/{c.ComplaintCode}",
                CreatedAt = DateTime.UtcNow
            });

            await _context.SaveChangesAsync();

            if (!string.IsNullOrWhiteSpace(spUser.Email))
            {
                _ = Task.Run(() => _emailService.SendAccountStatusNotificationAsync(spUser.Email, spUser.FullName, false));
            }

            return Ok(ToDto(c));
        }

        // ---------------- ADMIN BOOKING ISSUES (SP FR-SP-38) ----------------

        [Authorize(Roles = "Admin")]
        [HttpGet("admin/booking-issues")]
        public async Task<IActionResult> GetAllBookingIssues()
        {
            var issues = await _context.BookingIssues
                .OrderByDescending(i => i.Id)
                .ToListAsync();
            return Ok(issues);
        }

        [Authorize(Roles = "Admin")]
        [HttpPut("admin/booking-issues/{code}")]
        public async Task<IActionResult> UpdateBookingIssue(string code, [FromBody] UpdateComplaintRequest request)
        {
            var issue = await _context.BookingIssues.FirstOrDefaultAsync(i => i.IssueCode == code);
            if (issue == null) return NotFound(new { message = "Issue report not found." });

            issue.Status = string.IsNullOrWhiteSpace(request.Status) ? issue.Status : request.Status.Trim();
            issue.AdminNotes = request.Notes;

            _context.Notifications.Add(new AppNotification
            {
                UserId = issue.ProviderId,
                Title = $"Issue Update ({issue.IssueCode})",
                Message = $"Your reported booking issue ({issue.BookingCode}) is now {issue.Status}.{(string.IsNullOrWhiteSpace(issue.AdminNotes) ? "" : " Note: " + issue.AdminNotes)}",
                Type = "info",
                LinkUrl = $"/sp/bookings/{issue.BookingCode}",
                CreatedAt = DateTime.UtcNow
            });

            await _context.SaveChangesAsync();
            return Ok(issue);
        }
    }
}
