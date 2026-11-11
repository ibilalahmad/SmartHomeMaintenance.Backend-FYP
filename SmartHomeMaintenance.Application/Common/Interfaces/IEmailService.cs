using System.Threading.Tasks;

namespace SmartHomeMaintenance.Application.Common.Interfaces
{
    public interface IEmailService
    {
        Task SendEmailAsync(string toEmail, string subject, string htmlBody);
        Task SendPasswordResetCodeAsync(string toEmail, string recipientName, string code);
        Task SendRegistrationOtpAsync(string toEmail, string recipientName, string code, string userType);
        Task SendWelcomeEmailAsync(string toEmail, string recipientName, string userType);
        Task SendPasswordChangedNotificationAsync(string toEmail, string recipientName);
        Task SendAccountStatusNotificationAsync(string toEmail, string recipientName, bool isActive);
        Task SendProviderRejectedNotificationAsync(string toEmail, string recipientName);
        Task SendNotificationEmailAsync(
            string toEmail,
            string recipientName,
            string subject,
            string badge,
            string subtitle,
            string title,
            string messageBody,
            string? statusBanner = null,
            bool isWarning = false);
    }
}
