using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmartHomeMaintenance.Application.Common.Interfaces;
using SmartHomeMaintenance.Domain.Entities;
using SmartHomeMaintenance.Domain.Enums;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;

namespace SmartHomeMaintenance.API.Controllers
{
    public class CreateBookingRequest
    {
        public string ProviderId { get; set; } = string.Empty;
        public string ProviderName { get; set; } = string.Empty;
        public string ServiceType { get; set; } = string.Empty;
        public string Date { get; set; } = string.Empty;
        public string Time { get; set; } = string.Empty;
        public string Address { get; set; } = string.Empty;
        public string? Instructions { get; set; }
        public decimal Price { get; set; }
    }

    public class CancelBookingRequest
    {
        public string Reason { get; set; } = string.Empty;
    }

    public class RescheduleBookingRequest
    {
        public string Date { get; set; } = string.Empty;
        public string Time { get; set; } = string.Empty;
    }

    public class PayBookingRequest
    {
        public string Method { get; set; } = "Cash on Delivery";
        public string? Channel { get; set; }
        public string? ReferenceNo { get; set; }
        public string? ProofUrl { get; set; }
        public string? Note { get; set; }
    }

    public class VerifyPaymentRequest
    {
        public bool Approve { get; set; }
        public string? Note { get; set; }
    }

    public class UpdateBookingStatusRequest
    {
        public string Status { get; set; } = string.Empty;
    }

    public class ProposeTimeRequest
    {
        public string Date { get; set; } = string.Empty;
        public string Time { get; set; } = string.Empty;
        public string? Note { get; set; }
    }

    public class RespondTimeProposalRequest
    {
        public bool Accept { get; set; }
    }

    public class ReportBookingIssueRequest
    {
        public string IssueType { get; set; } = "General Issue";
        public string Description { get; set; } = string.Empty;
    }

    public class BookingDto
    {
        public string Id { get; set; } = string.Empty;
        public int CustomerId { get; set; }
        public string Customer { get; set; } = string.Empty;
        public string? Phone { get; set; }
        public string ProviderId { get; set; } = string.Empty;
        public string Provider { get; set; } = string.Empty;
        public string Service { get; set; } = string.Empty;
        public string When { get; set; } = string.Empty;
        public string Date { get; set; } = string.Empty;
        public decimal Price { get; set; }
        public string Status { get; set; } = string.Empty;
        public string Address { get; set; } = string.Empty;
        public string? Instructions { get; set; }
        public string PaymentStatus { get; set; } = "Pending";
        public string? PaymentMethod { get; set; }
        public string? PaymentChannel { get; set; }
        public string? PaymentReference { get; set; }
        public string? PaymentProofUrl { get; set; }
        public string? PaymentNote { get; set; }
        public DateTime? PaymentSubmittedAt { get; set; }
        public DateTime? PaymentVerifiedAt { get; set; }
        public string? ProposedWhen { get; set; }
        public string? ProposalNote { get; set; }
        public string? ProposalStatus { get; set; }
    }

    [Authorize]
    [ApiController]
    [Route("api/[controller]")]
    public class BookingsController : ControllerBase
    {
        private readonly IApplicationDbContext _context;

        public BookingsController(IApplicationDbContext context)
        {
            _context = context;
        }

        private int? GetCurrentUserId()
        {
            var idStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            return int.TryParse(idStr, out var id) ? id : null;
        }

        private static BookingDto ToDto(Booking b) => new BookingDto
        {
            Id = b.BookingCode,
            CustomerId = b.CustomerId,
            Customer = b.CustomerName,
            Phone = b.CustomerPhone,
            ProviderId = b.ProviderId.ToString(),
            Provider = b.ProviderName,
            Service = b.ServiceType,
            When = b.ScheduledWhen,
            Date = b.CreatedAt.ToString("o"),
            Price = b.Price,
            Status = b.Status,
            Address = b.Address,
            Instructions = b.Instructions,
            PaymentStatus = b.PaymentStatus,
            PaymentMethod = b.PaymentMethod,
            PaymentChannel = b.PaymentChannel,
            PaymentReference = b.PaymentReference,
            PaymentProofUrl = b.PaymentProofUrl,
            PaymentNote = b.PaymentNote,
            PaymentSubmittedAt = b.PaymentSubmittedAt,
            PaymentVerifiedAt = b.PaymentVerifiedAt,
            ProposedWhen = b.ProposedWhen,
            ProposalNote = b.ProposalNote,
            ProposalStatus = b.ProposalStatus
        };

        private Task<Booking?> FindBookingAsync(string code)
        {
            var trimmed = (code ?? string.Empty).Trim();
            if (int.TryParse(trimmed, out var numericId))
            {
                return _context.Bookings.FirstOrDefaultAsync(b => b.BookingCode == trimmed || b.Id == numericId);
            }
            return _context.Bookings.FirstOrDefaultAsync(b => b.BookingCode == trimmed);
        }

        private static bool TryParseScheduledDateTime(string? scheduledWhen, out DateTime result)
        {
            result = default;
            if (string.IsNullOrWhiteSpace(scheduledWhen)) return false;
            return DateTime.TryParse(scheduledWhen.Trim(), CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out result)
                || DateTime.TryParse(scheduledWhen.Trim(), out result);
        }

        private static bool ValidateScheduleSlot(string? date, string? time, out string formattedWhen, out string? errorMessage)
        {
            formattedWhen = $"{date?.Trim()} {time?.Trim()}".Trim();
            errorMessage = null;

            if (!TryParseScheduledDateTime(formattedWhen, out var scheduled))
            {
                return true; // Allow non-standard display strings if unparseable, otherwise validate strictly
            }

            if (scheduled.TimeOfDay < TimeSpan.FromHours(9) || scheduled.TimeOfDay > TimeSpan.FromHours(21))
            {
                errorMessage = "Please select time between 9 AM and 9 PM.";
                return false;
            }

            if (scheduled < DateTime.Now.AddHours(2))
            {
                errorMessage = "Booking must be scheduled at least 2 hours in advance.";
                return false;
            }

            return true;
        }

        private async Task<bool> HasSlotConflictAsync(int providerId, string scheduledWhen, int? excludeBookingId = null)
        {
            var candidates = await _context.Bookings
                .Where(b => b.ProviderId == providerId
                    && b.Status != "Cancelled"
                    && (!excludeBookingId.HasValue || b.Id != excludeBookingId.Value))
                .Select(b => b.ScheduledWhen)
                .ToListAsync();

            if (TryParseScheduledDateTime(scheduledWhen, out var targetDt))
            {
                foreach (var c in candidates)
                {
                    if (string.Equals(c?.Trim(), scheduledWhen.Trim(), StringComparison.OrdinalIgnoreCase))
                        return true;
                    if (TryParseScheduledDateTime(c, out var existingDt) && Math.Abs((existingDt - targetDt).TotalMinutes) < 30)
                        return true;
                }
                return false;
            }

            return candidates.Any(c => string.Equals(c?.Trim(), scheduledWhen.Trim(), StringComparison.OrdinalIgnoreCase));
        }

        private void AddNotification(int userId, string title, string message, string type, string? linkUrl = null)
        {
            _context.Notifications.Add(new AppNotification
            {
                UserId = userId,
                Title = title,
                Message = message,
                Type = type,
                LinkUrl = linkUrl,
                IsRead = false,
                CreatedAt = DateTime.UtcNow
            });
        }

        [AllowAnonymous]
        [HttpGet("provider/{providerId:int}/booked-slots")]
        public async Task<IActionResult> GetProviderBookedSlots(int providerId)
        {
            var slots = await _context.Bookings
                .Where(b => b.ProviderId == providerId && b.Status != "Cancelled" && b.Status != "Completed")
                .Select(b => b.ScheduledWhen)
                .ToListAsync();

            return Ok(slots);
        }

        // ---------------- CUSTOMER ENDPOINTS ----------------

        [Authorize(Roles = "Customer,Admin")]
        [HttpGet("customer/my")]
        public async Task<IActionResult> GetMyCustomerBookings()
        {
            var userId = GetCurrentUserId();
            if (!userId.HasValue) return Unauthorized();

            var list = await _context.Bookings
                .Where(b => b.CustomerId == userId.Value)
                .OrderByDescending(b => b.Id)
                .ToListAsync();

            return Ok(list.Select(ToDto));
        }

        [Authorize(Roles = "Customer")]
        [HttpPost("customer")]
        public async Task<IActionResult> CreateBooking([FromBody] CreateBookingRequest request)
        {
            var userId = GetCurrentUserId();
            if (!userId.HasValue) return Unauthorized();

            if (!int.TryParse(request.ProviderId, out var providerId) || providerId <= 0)
            {
                return BadRequest(new { message = "Invalid service provider." });
            }

            var customerUser = await _context.Users
                .Include(u => u.Customer)
                .FirstOrDefaultAsync(u => u.Id == userId.Value);

            if (customerUser == null || customerUser.Customer == null)
            {
                return NotFound(new { message = "Customer profile not found." });
            }

            var providerSp = await _context.ServiceProviders
                .Include(sp => sp.User)
                .FirstOrDefaultAsync(sp => sp.ProviderId == providerId);

            if (providerSp == null || providerSp.User == null)
            {
                return NotFound(new { message = "Service provider not found." });
            }

            if (!providerSp.IsAvailable || !providerSp.User.IsActive || !providerSp.IsApproved)
            {
                return BadRequest(new { message = "This service provider is currently unavailable for new bookings." });
            }

            if (!ValidateScheduleSlot(request.Date, request.Time, out var scheduledWhen, out var scheduleError))
            {
                return BadRequest(new { message = scheduleError });
            }

            if (await HasSlotConflictAsync(providerId, scheduledWhen))
            {
                return BadRequest(new { message = "Provider is already booked at this date and time slot." });
            }

            var activeCount = await _context.Bookings.CountAsync(b =>
                b.CustomerId == userId.Value &&
                (b.Status == "Pending" || b.Status == "Confirmed" || b.Status == "In Progress"));

            if (activeCount >= 3)
            {
                return BadRequest(new { message = "You have reached the maximum limit of 3 active bookings." });
            }

            // Generate unique BK-XXXX code
            string code;
            do
            {
                code = $"BK-{Random.Shared.Next(1000, 10000)}";
            } while (await _context.Bookings.AnyAsync(b => b.BookingCode == code));

            var booking = new Booking
            {
                BookingCode = code,
                CustomerId = userId.Value,
                CustomerName = customerUser.FullName,
                CustomerPhone = customerUser.PhoneNumber,
                ProviderId = providerId,
                ProviderName = string.IsNullOrWhiteSpace(request.ProviderName) ? providerSp.User.FullName : request.ProviderName,
                ServiceType = request.ServiceType,
                ScheduledWhen = scheduledWhen,
                Price = request.Price > 0 ? request.Price : providerSp.HourlyRate,
                Status = "Pending",
                Address = request.Address,
                Instructions = request.Instructions,
                PaymentStatus = "Pending",
                CreatedAt = DateTime.UtcNow
            };

            _context.Bookings.Add(booking);
            AddNotification(userId.Value, "Booking Submitted", $"Your booking {code} with {booking.ProviderName} for {scheduledWhen} has been submitted.", "success", $"/customer/bookings/{code}");
            AddNotification(providerId, "New Booking Request", $"{customerUser.FullName} requested {booking.ServiceType} on {scheduledWhen} ({code}).", "info", $"/sp/bookings/{code}");
            await _context.SaveChangesAsync();

            return Ok(ToDto(booking));
        }

        [Authorize(Roles = "Customer,Admin")]
        [HttpGet("customer/detail/{code}")]
        public async Task<IActionResult> GetCustomerBookingById(string code)
        {
            var booking = await FindBookingAsync(code);
            if (booking == null) return NotFound(new { message = "Booking not found." });

            var userId = GetCurrentUserId();
            if (!User.IsInRole("Admin") && (!userId.HasValue || booking.CustomerId != userId.Value))
            {
                return Forbid();
            }

            return Ok(ToDto(booking));
        }

        [Authorize(Roles = "Customer,Admin")]
        [HttpPut("customer/{code}/cancel")]
        public async Task<IActionResult> CancelCustomerBooking(string code, [FromBody] CancelBookingRequest request)
        {
            var booking = await FindBookingAsync(code);
            if (booking == null) return NotFound(new { message = "Booking not found." });

            var userId = GetCurrentUserId();
            if (!User.IsInRole("Admin") && (!userId.HasValue || booking.CustomerId != userId.Value))
            {
                return Forbid();
            }

            if (!User.IsInRole("Admin") && TryParseScheduledDateTime(booking.ScheduledWhen, out var scheduled))
            {
                if (scheduled > DateTime.Now && (scheduled - DateTime.Now).TotalHours < 2)
                {
                    var spUser = await _context.Users.FirstOrDefaultAsync(u => u.Id == booking.ProviderId);
                    var spPhone = spUser?.PhoneNumber ?? "N/A";
                    return BadRequest(new
                    {
                        message = $"Cancellation not allowed within 2 hours of service time. Please contact the service provider directly ({spPhone}).",
                        providerPhone = spPhone
                    });
                }
            }

            booking.Status = "Cancelled";
            booking.CancelReason = request?.Reason;
            AddNotification(booking.ProviderId, "Booking Cancelled", $"Customer {booking.CustomerName} cancelled booking {booking.BookingCode}.", "warning", $"/sp/bookings/{booking.BookingCode}");
            await _context.SaveChangesAsync();

            return Ok(ToDto(booking));
        }

        [Authorize(Roles = "Customer,Admin")]
        [HttpPut("customer/{code}/reschedule")]
        public async Task<IActionResult> RescheduleCustomerBooking(string code, [FromBody] RescheduleBookingRequest request)
        {
            var booking = await FindBookingAsync(code);
            if (booking == null) return NotFound(new { message = "Booking not found." });

            var userId = GetCurrentUserId();
            if (!User.IsInRole("Admin") && (!userId.HasValue || booking.CustomerId != userId.Value))
            {
                return Forbid();
            }

            if (!ValidateScheduleSlot(request.Date, request.Time, out var scheduledWhen, out var scheduleError))
            {
                return BadRequest(new { message = scheduleError });
            }

            if (await HasSlotConflictAsync(booking.ProviderId, scheduledWhen, booking.Id))
            {
                return BadRequest(new { message = "Provider is already booked at this date and time slot." });
            }

            booking.ScheduledWhen = scheduledWhen;
            booking.ProposedWhen = null;
            booking.ProposalNote = null;
            booking.ProposalStatus = null;
            booking.ReminderSent = false;
            AddNotification(booking.ProviderId, "Booking Rescheduled", $"{booking.CustomerName} rescheduled {booking.BookingCode} to {scheduledWhen}.", "info", $"/sp/bookings/{booking.BookingCode}");
            await _context.SaveChangesAsync();

            return Ok(ToDto(booking));
        }

        [Authorize(Roles = "Customer,Admin")]
        [HttpPut("customer/{code}/respond-time")]
        public async Task<IActionResult> RespondToTimeProposal(string code, [FromBody] RespondTimeProposalRequest request)
        {
            var booking = await FindBookingAsync(code);
            if (booking == null) return NotFound(new { message = "Booking not found." });

            var userId = GetCurrentUserId();
            if (!User.IsInRole("Admin") && (!userId.HasValue || booking.CustomerId != userId.Value))
            {
                return Forbid();
            }

            if (booking.ProposalStatus != "PendingCustomerConfirmation" || string.IsNullOrWhiteSpace(booking.ProposedWhen))
            {
                return BadRequest(new { message = "There is no pending time change proposal for this booking." });
            }

            if (request.Accept)
            {
                booking.ScheduledWhen = booking.ProposedWhen;
                booking.ProposedWhen = null;
                booking.ProposalNote = null;
                booking.ProposalStatus = "Accepted";
                booking.ReminderSent = false;
                if (booking.Status == "Pending")
                {
                    booking.Status = "Confirmed";
                }
                AddNotification(booking.ProviderId, "Time Proposal Accepted", $"{booking.CustomerName} accepted your proposed slot {booking.ScheduledWhen} for {booking.BookingCode}.", "success", $"/sp/bookings/{booking.BookingCode}");
            }
            else
            {
                booking.ProposedWhen = null;
                booking.ProposalNote = null;
                booking.ProposalStatus = "Declined";
                AddNotification(booking.ProviderId, "Time Proposal Declined", $"{booking.CustomerName} declined the proposed time for {booking.BookingCode}.", "warning", $"/sp/bookings/{booking.BookingCode}");
            }

            await _context.SaveChangesAsync();
            return Ok(ToDto(booking));
        }

        [Authorize(Roles = "Customer,Admin")]
        [HttpPut("customer/{code}/pay")]
        public async Task<IActionResult> PayCustomerBooking(string code, [FromBody] PayBookingRequest request)
        {
            var booking = await FindBookingAsync(code);
            if (booking == null) return NotFound(new { message = "Booking not found." });

            var userId = GetCurrentUserId();
            if (!User.IsInRole("Admin") && (!userId.HasValue || booking.CustomerId != userId.Value))
            {
                return Forbid();
            }

            if (booking.Status == "Cancelled")
            {
                return BadRequest(new { message = "Cannot submit payment for a cancelled booking." });
            }

            if (booking.PaymentStatus == "Paid")
            {
                return BadRequest(new { message = "This booking payment has already been approved and completed." });
            }

            var rawMethod = (request?.Method ?? string.Empty).Trim().ToLowerInvariant();
            string normalizedMethod;
            if (rawMethod == "cash" || rawMethod == "cash on delivery" || rawMethod == "cod")
            {
                normalizedMethod = "Cash on Delivery";
            }
            else if (rawMethod == "bank" || rawMethod == "bank transfer" || rawMethod == "jazzcash" || rawMethod == "easypaisa")
            {
                normalizedMethod = "Bank Transfer";
            }
            else
            {
                return BadRequest(new { message = "Only Cash on Delivery and Bank Transfer (JazzCash / EasyPaisa) are supported." });
            }

            if (normalizedMethod == "Bank Transfer")
            {
                var rawChannel = (request?.Channel ?? string.Empty).Trim();
                var channel = rawChannel.Equals("EasyPaisa", StringComparison.OrdinalIgnoreCase)
                    ? "EasyPaisa"
                    : rawChannel.Equals("JazzCash", StringComparison.OrdinalIgnoreCase)
                        ? "JazzCash"
                        : null;

                if (channel == null)
                {
                    return BadRequest(new { message = "Please select either JazzCash or EasyPaisa." });
                }

                var referenceNo = request?.ReferenceNo?.Trim();
                if (string.IsNullOrWhiteSpace(referenceNo) || referenceNo.Length < 3)
                {
                    return BadRequest(new { message = "Please enter the payment transaction/reference number." });
                }

                var proofUrl = request?.ProofUrl?.Trim();
                if (string.IsNullOrWhiteSpace(proofUrl))
                {
                    return BadRequest(new { message = "Please upload the payment receipt screenshot picture." });
                }

                booking.PaymentMethod = "Bank Transfer";
                booking.PaymentChannel = channel;
                booking.PaymentReference = referenceNo;
                booking.PaymentProofUrl = proofUrl;
                booking.PaymentNote = string.IsNullOrWhiteSpace(request?.Note) ? null : request.Note.Trim();
                booking.PaymentStatus = "PendingVerification";
                booking.PaymentSubmittedAt = DateTime.UtcNow;
                booking.PaymentVerifiedAt = null;

                AddNotification(
                    booking.ProviderId,
                    "Payment Proof Submitted",
                    $"{booking.CustomerName} submitted {channel} payment (Ref: {referenceNo}) for booking {booking.BookingCode}. Please review and approve.",
                    "info",
                    $"/sp/bookings/{booking.BookingCode}");
            }
            else
            {
                booking.PaymentMethod = "Cash on Delivery";
                booking.PaymentChannel = null;
                booking.PaymentReference = null;
                booking.PaymentProofUrl = null;
                booking.PaymentNote = string.IsNullOrWhiteSpace(request?.Note) ? null : request.Note.Trim();
                booking.PaymentStatus = "PendingVerification";
                booking.PaymentSubmittedAt = DateTime.UtcNow;
                booking.PaymentVerifiedAt = null;

                AddNotification(
                    booking.ProviderId,
                    "Cash on Delivery Selected",
                    $"{booking.CustomerName} chose Cash on Delivery for booking {booking.BookingCode}. Please confirm/approve once cash is collected.",
                    "info",
                    $"/sp/bookings/{booking.BookingCode}");
            }

            await _context.SaveChangesAsync();
            return Ok(ToDto(booking));
        }

        // ---------------- SERVICE PROVIDER ENDPOINTS ----------------

        [Authorize(Roles = "ServiceProvider,Admin")]
        [HttpGet("provider/my")]
        public async Task<IActionResult> GetMyProviderBookings()
        {
            var userId = GetCurrentUserId();
            if (!userId.HasValue) return Unauthorized();

            var list = await _context.Bookings
                .Where(b => b.ProviderId == userId.Value)
                .OrderByDescending(b => b.Id)
                .ToListAsync();

            return Ok(list.Select(ToDto));
        }

        [Authorize(Roles = "ServiceProvider,Admin")]
        [HttpGet("provider/{providerId:int}")]
        public async Task<IActionResult> GetProviderBookings(int providerId)
        {
            var userId = GetCurrentUserId();
            if (!User.IsInRole("Admin") && (!userId.HasValue || userId.Value != providerId))
            {
                return Forbid();
            }

            var list = await _context.Bookings
                .Where(b => b.ProviderId == providerId)
                .OrderByDescending(b => b.Id)
                .ToListAsync();

            return Ok(list.Select(ToDto));
        }

        [Authorize(Roles = "ServiceProvider,Admin")]
        [HttpGet("provider/detail/{code}")]
        public async Task<IActionResult> GetProviderBookingById(string code)
        {
            var booking = await FindBookingAsync(code);
            if (booking == null) return NotFound(new { message = "Booking not found." });

            var userId = GetCurrentUserId();
            if (!User.IsInRole("Admin") && (!userId.HasValue || booking.ProviderId != userId.Value))
            {
                return Forbid();
            }

            return Ok(ToDto(booking));
        }

        [Authorize(Roles = "ServiceProvider,Admin")]
        [HttpPut("provider/{code}/status")]
        public async Task<IActionResult> UpdateProviderBookingStatus(string code, [FromBody] UpdateBookingStatusRequest request)
        {
            var booking = await FindBookingAsync(code);
            if (booking == null) return NotFound(new { message = "Booking not found." });

            var userId = GetCurrentUserId();
            if (!User.IsInRole("Admin") && (!userId.HasValue || booking.ProviderId != userId.Value))
            {
                return Forbid();
            }

            var validTransitions = new Dictionary<string, string[]>
            {
                ["Pending"] = new[] { "Confirmed", "Cancelled" },
                ["Confirmed"] = new[] { "In Progress", "Completed", "Cancelled" },
                ["In Progress"] = new[] { "Completed" },
                ["Completed"] = Array.Empty<string>(),
                ["Cancelled"] = Array.Empty<string>()
            };

            if (!validTransitions.TryGetValue(booking.Status, out var allowed) || !allowed.Contains(request.Status))
            {
                return BadRequest(new { message = $"Cannot transition from {booking.Status} to {request.Status}" });
            }

            booking.Status = request.Status;
            if (booking.ProposalStatus == "PendingCustomerConfirmation")
            {
                booking.ProposedWhen = null;
                booking.ProposalNote = null;
                booking.ProposalStatus = null;
            }
            if (request.Status == "Completed")
            {
                var sp = await _context.ServiceProviders.FirstOrDefaultAsync(s => s.ProviderId == booking.ProviderId);
                if (sp != null)
                {
                    sp.TotalBookings += 1;
                }
            }

            var notifType = request.Status == "Cancelled" ? "warning" : "success";
            AddNotification(booking.CustomerId, $"Booking {request.Status}", $"Your booking {booking.BookingCode} ({booking.ServiceType}) status is now {request.Status}.", notifType, $"/customer/bookings/{booking.BookingCode}");

            await _context.SaveChangesAsync();
            return Ok(ToDto(booking));
        }

        [Authorize(Roles = "ServiceProvider,Admin")]
        [HttpPut("provider/{code}/verify-payment")]
        public async Task<IActionResult> VerifyProviderPayment(string code, [FromBody] VerifyPaymentRequest request)
        {
            var booking = await FindBookingAsync(code);
            if (booking == null) return NotFound(new { message = "Booking not found." });

            var userId = GetCurrentUserId();
            if (!User.IsInRole("Admin") && (!userId.HasValue || booking.ProviderId != userId.Value))
            {
                return Forbid();
            }

            if (booking.Status == "Cancelled")
            {
                return BadRequest(new { message = "Cannot verify payment for a cancelled booking." });
            }

            if (request.Approve)
            {
                if (string.IsNullOrWhiteSpace(booking.PaymentMethod))
                {
                    booking.PaymentMethod = "Cash on Delivery";
                }
                booking.PaymentStatus = "Paid";
                booking.PaymentVerifiedAt = DateTime.UtcNow;
                booking.PaymentNote = string.IsNullOrWhiteSpace(request.Note) ? null : request.Note.Trim();

                AddNotification(
                    booking.CustomerId,
                    "Payment Approved",
                    $"{booking.ProviderName} approved your {booking.PaymentMethod} payment for booking {booking.BookingCode}.",
                    "success",
                    $"/customer/bookings/{booking.BookingCode}");
            }
            else
            {
                var rejectNote = string.IsNullOrWhiteSpace(request.Note)
                    ? "Payment details or screenshot could not be verified. Please check and re-submit."
                    : request.Note.Trim();

                booking.PaymentStatus = "Rejected";
                booking.PaymentVerifiedAt = null;
                booking.PaymentNote = rejectNote;

                AddNotification(
                    booking.CustomerId,
                    "Payment Rejected",
                    $"{booking.ProviderName} could not verify payment for booking {booking.BookingCode}: {rejectNote}",
                    "warning",
                    $"/customer/bookings/{booking.BookingCode}");
            }

            await _context.SaveChangesAsync();
            return Ok(ToDto(booking));
        }

        [Authorize(Roles = "ServiceProvider,Admin")]
        [HttpPut("provider/{code}/propose-time")]
        public async Task<IActionResult> ProposeNewTime(string code, [FromBody] ProposeTimeRequest request)
        {
            var booking = await FindBookingAsync(code);
            if (booking == null) return NotFound(new { message = "Booking not found." });

            var userId = GetCurrentUserId();
            if (!User.IsInRole("Admin") && (!userId.HasValue || booking.ProviderId != userId.Value))
            {
                return Forbid();
            }

            if (booking.Status != "Pending" && booking.Status != "Confirmed")
            {
                return BadRequest(new { message = $"Cannot suggest a new time when booking is {booking.Status}." });
            }

            if (string.IsNullOrWhiteSpace(request?.Date) || string.IsNullOrWhiteSpace(request?.Time))
            {
                return BadRequest(new { message = "Both date and time are required." });
            }

            booking.ProposedWhen = $"{request.Date.Trim()} {request.Time.Trim()}";
            booking.ProposalNote = string.IsNullOrWhiteSpace(request.Note) ? null : request.Note.Trim();
            booking.ProposalStatus = "PendingCustomerConfirmation";
            AddNotification(booking.CustomerId, "New Time Proposed", $"{booking.ProviderName} suggested {booking.ProposedWhen} for booking {booking.BookingCode}. Please confirm.", "info", $"/customer/bookings/{booking.BookingCode}");

            await _context.SaveChangesAsync();
            return Ok(ToDto(booking));
        }

        [Authorize(Roles = "ServiceProvider,Admin")]
        [HttpPost("provider/{code}/report-issue")]
        public async Task<IActionResult> ReportProviderBookingIssue(string code, [FromBody] ReportBookingIssueRequest request)
        {
            var booking = await FindBookingAsync(code);
            if (booking == null) return NotFound(new { message = "Booking not found." });

            var userId = GetCurrentUserId();
            if (!User.IsInRole("Admin") && (!userId.HasValue || booking.ProviderId != userId.Value))
            {
                return Forbid();
            }

            if (request == null || string.IsNullOrWhiteSpace(request.Description) || request.Description.Trim().Length < 10)
            {
                return BadRequest(new { message = "Please describe the issue (at least 10 characters)." });
            }

            string issueCode;
            do
            {
                issueCode = $"ISS-{Random.Shared.Next(1000, 10000)}";
            } while (await _context.BookingIssues.AnyAsync(i => i.IssueCode == issueCode));

            var issue = new BookingIssue
            {
                IssueCode = issueCode,
                BookingCode = booking.BookingCode,
                ProviderId = booking.ProviderId,
                ProviderName = booking.ProviderName,
                CustomerId = booking.CustomerId,
                CustomerName = booking.CustomerName,
                IssueType = string.IsNullOrWhiteSpace(request.IssueType) ? "General Issue" : request.IssueType.Trim(),
                Description = request.Description.Trim(),
                Status = "Open",
                CreatedAt = DateTime.UtcNow
            };

            _context.BookingIssues.Add(issue);

            var admins = await _context.Users.Where(u => u.UserType == UserType.Admin && u.IsActive).ToListAsync();
            foreach (var admin in admins)
            {
                AddNotification(admin.Id, "SP Reported Booking Issue", $"{booking.ProviderName} reported {issue.IssueType} on booking {booking.BookingCode} ({issueCode}).", "warning", "/admin/complaints");
            }

            await _context.SaveChangesAsync();
            return Ok(new { success = true, issueCode, issue });
        }

        [Authorize(Roles = "Admin")]
        [HttpGet("admin/all")]
        public async Task<IActionResult> GetAllBookingsForAdmin()
        {
            var list = await _context.Bookings
                .OrderByDescending(b => b.Id)
                .ToListAsync();

            return Ok(list.Select(ToDto));
        }

        [Authorize(Roles = "Admin")]
        [HttpPut("admin/{code}/cancel")]
        public async Task<IActionResult> CancelBookingAsAdmin(string code)
        {
            var booking = await FindBookingAsync(code);
            if (booking == null) return NotFound(new { message = "Booking not found." });

            booking.Status = "Cancelled";
            await _context.SaveChangesAsync();
            return Ok(ToDto(booking));
        }
    }
}
