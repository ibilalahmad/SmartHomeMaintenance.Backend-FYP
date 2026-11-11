using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmartHomeMaintenance.Application.Common.Interfaces;
using SmartHomeMaintenance.Domain.Entities;
using SmartHomeMaintenance.Domain.Enums;
using System;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;

namespace SmartHomeMaintenance.API.Controllers
{
    public class NotificationEventRequest
    {
        public int? RecipientUserId { get; set; }
        public string? RecipientName { get; set; }
        public string? RecipientRole { get; set; } // "Customer" or "ServiceProvider"
        public string Subject { get; set; } = string.Empty;
        public string Badge { get; set; } = "SHMS NOTIFICATION";
        public string Subtitle { get; set; } = "Activity Update";
        public string Title { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public string? StatusBanner { get; set; }
        public bool IsWarning { get; set; } = false;
        public string? LinkUrl { get; set; }
    }

    [Authorize]
    [ApiController]
    [Route("api/[controller]")]
    public class NotificationsController : ControllerBase
    {
        private readonly IApplicationDbContext _context;
        private readonly IEmailService _emailService;

        public NotificationsController(IApplicationDbContext context, IEmailService emailService)
        {
            _context = context;
            _emailService = emailService;
        }

        private int? GetCurrentUserId()
        {
            var idStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            return int.TryParse(idStr, out var id) ? id : null;
        }

        [HttpGet("my")]
        public async Task<IActionResult> GetMyNotifications([FromQuery] bool unreadOnly = false)
        {
            var userId = GetCurrentUserId();
            if (!userId.HasValue) return Unauthorized();

            var cutoff = DateTime.UtcNow.AddDays(-30);
            var query = _context.Notifications
                .Where(n => n.UserId == userId.Value && n.CreatedAt >= cutoff);

            if (unreadOnly)
            {
                query = query.Where(n => !n.IsRead);
            }

            var list = await query
                .OrderByDescending(n => n.CreatedAt)
                .Take(100)
                .Select(n => new
                {
                    id = n.Id.ToString(),
                    title = n.Title,
                    message = n.Message,
                    type = n.Type,
                    link = n.LinkUrl,
                    read = n.IsRead,
                    date = n.CreatedAt.ToString("o")
                })
                .ToListAsync();

            return Ok(list);
        }

        [HttpPut("{id:int}/read")]
        public async Task<IActionResult> MarkAsRead(int id)
        {
            var userId = GetCurrentUserId();
            if (!userId.HasValue) return Unauthorized();

            var notif = await _context.Notifications.FirstOrDefaultAsync(n => n.Id == id && n.UserId == userId.Value);
            if (notif == null) return NotFound();

            notif.IsRead = true;
            await _context.SaveChangesAsync();
            return Ok(new { success = true });
        }

        [HttpPut("read-all")]
        public async Task<IActionResult> MarkAllAsRead()
        {
            var userId = GetCurrentUserId();
            if (!userId.HasValue) return Unauthorized();

            var unread = await _context.Notifications
                .Where(n => n.UserId == userId.Value && !n.IsRead)
                .ToListAsync();

            foreach (var n in unread)
            {
                n.IsRead = true;
            }

            await _context.SaveChangesAsync();
            return Ok(new { success = true, markedCount = unread.Count });
        }

        [HttpPost("notify-event")]
        public async Task<IActionResult> NotifyEvent([FromBody] NotificationEventRequest request)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.Subject) || string.IsNullOrWhiteSpace(request.Message))
            {
                return BadRequest(new { message = "Subject and message are required." });
            }

            User? recipient = null;

            if (request.RecipientUserId.HasValue && request.RecipientUserId.Value > 0)
            {
                recipient = await _context.Users.FirstOrDefaultAsync(u => u.Id == request.RecipientUserId.Value);
            }
            else if (!string.IsNullOrWhiteSpace(request.RecipientName))
            {
                var nameLower = request.RecipientName.Trim().ToLower();
                if (!string.IsNullOrWhiteSpace(request.RecipientRole) &&
                    Enum.TryParse<UserType>(request.RecipientRole, true, out var roleEnum))
                {
                    recipient = await _context.Users
                        .FirstOrDefaultAsync(u => u.UserType == roleEnum && u.FullName.ToLower() == nameLower);
                }
                else
                {
                    recipient = await _context.Users
                        .FirstOrDefaultAsync(u => u.FullName.ToLower() == nameLower);
                }
            }

            if (recipient == null)
            {
                return Ok(new { success = false, message = "Recipient not found." });
            }

            // Persist notification in SQL Server
            _context.Notifications.Add(new AppNotification
            {
                UserId = recipient.Id,
                Title = string.IsNullOrWhiteSpace(request.Title) ? request.Subject : request.Title,
                Message = request.Message,
                Type = request.IsWarning ? "warning" : "info",
                LinkUrl = request.LinkUrl ?? (recipient.UserType == UserType.ServiceProvider ? "/sp/notifications" : "/customer/notifications"),
                IsRead = false,
                CreatedAt = DateTime.UtcNow
            });
            await _context.SaveChangesAsync();

            if (!string.IsNullOrWhiteSpace(recipient.Email))
            {
                var targetEmail = recipient.Email;
                var targetName = recipient.FullName;

                try
                {
                    _ = Task.Run(() => _emailService.SendNotificationEmailAsync(
                        targetEmail,
                        targetName,
                        request.Subject,
                        request.Badge,
                        request.Subtitle,
                        request.Title,
                        request.Message,
                        request.StatusBanner,
                        request.IsWarning
                    ));
                }
                catch { /* Non-blocking */ }
            }

            return Ok(new { success = true, message = "Notification saved and email queued." });
        }
    }
}
