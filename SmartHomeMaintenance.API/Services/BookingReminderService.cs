using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SmartHomeMaintenance.Application.Common.Interfaces;
using SmartHomeMaintenance.Domain.Entities;
using System;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace SmartHomeMaintenance.API.Services
{
    /// <summary>
    /// Periodically checks confirmed bookings and dispatches the 2-hour advance reminder (FR-CUST-84).
    /// </summary>
    public class BookingReminderService : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<BookingReminderService> _logger;

        public BookingReminderService(IServiceScopeFactory scopeFactory, ILogger<BookingReminderService> logger)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            // Initial short delay to let startup finish
            await Task.Delay(TimeSpan.FromSeconds(15), stoppingToken);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await ProcessDueRemindersAsync(stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error checking 2-hour booking reminders.");
                }

                await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);
            }
        }

        private async Task ProcessDueRemindersAsync(CancellationToken ct)
        {
            using var scope = _scopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
            var emailService = scope.ServiceProvider.GetRequiredService<IEmailService>();

            var confirmedBookings = await context.Bookings
                .Where(b => b.Status == "Confirmed" && !b.ReminderSent)
                .ToListAsync(ct);

            if (confirmedBookings.Count == 0) return;

            var now = DateTime.Now;
            var changed = false;

            foreach (var b in confirmedBookings)
            {
                if (!TryParseScheduledDateTime(b.ScheduledWhen, out var scheduled)) continue;

                var diff = scheduled - now;
                if (diff > TimeSpan.Zero && diff <= TimeSpan.FromHours(2.25))
                {
                    b.ReminderSent = true;
                    changed = true;

                    context.Notifications.Add(new AppNotification
                    {
                        UserId = b.CustomerId,
                        Title = $"Upcoming Service Reminder ({b.BookingCode})",
                        Message = $"Your {b.ServiceType} appointment with {b.ProviderName} starts soon at {b.ScheduledWhen}.",
                        Type = "info",
                        LinkUrl = $"/customer/bookings/{b.BookingCode}",
                        CreatedAt = DateTime.UtcNow
                    });

                    context.Notifications.Add(new AppNotification
                    {
                        UserId = b.ProviderId,
                        Title = $"Upcoming Job Reminder ({b.BookingCode})",
                        Message = $"Scheduled {b.ServiceType} visit for {b.CustomerName} starts soon at {b.ScheduledWhen}.",
                        Type = "info",
                        LinkUrl = $"/sp/bookings/{b.BookingCode}",
                        CreatedAt = DateTime.UtcNow
                    });

                    var custUser = await context.Users.FirstOrDefaultAsync(u => u.Id == b.CustomerId, ct);
                    if (custUser != null && !string.IsNullOrWhiteSpace(custUser.Email))
                    {
                        _ = Task.Run(() => emailService.SendNotificationEmailAsync(
                            custUser.Email, custUser.FullName,
                            $"Reminder: {b.ServiceType} in ~2 Hours ({b.BookingCode}) - SHMS",
                            "SERVICE REMINDER", "Upcoming Appointment",
                            $"Your booking {b.BookingCode} is coming up soon",
                            $"Service Provider {b.ProviderName} is scheduled for {b.ScheduledWhen} at {b.Address}.",
                            $"Booking ID: {b.BookingCode} • Confirmed", false));
                    }
                }
            }

            if (changed)
            {
                await context.SaveChangesAsync(ct);
            }
        }

        private static bool TryParseScheduledDateTime(string when, out DateTime parsed)
        {
            parsed = default;
            if (string.IsNullOrWhiteSpace(when)) return false;

            string[] formats = { "yyyy-MM-dd HH:mm", "yyyy-MM-dd hh:mm tt", "yyyy-MM-dd h:mm tt", "yyyy-MM-dd H:mm" };
            if (DateTime.TryParseExact(when.Trim(), formats, CultureInfo.InvariantCulture, DateTimeStyles.AllowWhiteSpaces, out parsed))
                return true;

            return DateTime.TryParse(when.Trim(), CultureInfo.InvariantCulture, DateTimeStyles.AllowWhiteSpaces, out parsed);
        }
    }
}
