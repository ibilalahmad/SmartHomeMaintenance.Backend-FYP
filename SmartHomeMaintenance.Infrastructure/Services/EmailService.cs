using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MimeKit;
using SmartHomeMaintenance.Application.Common.Interfaces;
using System;
using System.Threading.Tasks;

namespace SmartHomeMaintenance.Infrastructure.Services
{
    public class EmailService : IEmailService
    {
        private readonly IConfiguration _config;
        private readonly ILogger<EmailService> _logger;

        public EmailService(IConfiguration config, ILogger<EmailService> logger)
        {
            _config = config;
            _logger = logger;
        }

        public async Task SendEmailAsync(string toEmail, string subject, string htmlBody)
        {
            var senderEmail = _config["EmailSettings:SenderEmail"] ?? "ibilalahmad.dev@gmail.com";
            var senderName = _config["EmailSettings:SenderName"] ?? "Smart Home Maintenance Service (SHMS)";
            var smtpServer = _config["EmailSettings:SmtpServer"] ?? "smtp.gmail.com";
            var smtpPortStr = _config["EmailSettings:SmtpPort"] ?? "465";
            var username = _config["EmailSettings:Username"] ?? "ibilalahmad.dev@gmail.com";
            var password = _config["EmailSettings:Password"];
            var smtpPort = int.TryParse(smtpPortStr, out var p) ? p : 465;

            var cleanPassword = password?.Replace(" ", "").Trim();

            if (string.IsNullOrWhiteSpace(cleanPassword))
            {
                _logger.LogWarning("EmailSettings:Password is empty. Real email sending skipped. Simulated email to: {ToEmail}, Subject: '{Subject}'", toEmail, subject);
                return;
            }

            try
            {
                var message = new MimeMessage();
                message.From.Add(new MailboxAddress(senderName, senderEmail));
                message.To.Add(new MailboxAddress(toEmail.Trim(), toEmail.Trim()));
                message.Subject = subject;

                var bodyBuilder = new BodyBuilder
                {
                    HtmlBody = htmlBody
                };
                message.Body = bodyBuilder.ToMessageBody();

                using var client = new SmtpClient
                {
                    Timeout = 10000
                };

                var socketOption = smtpPort == 465 
                    ? SecureSocketOptions.SslOnConnect 
                    : SecureSocketOptions.StartTls;

                await client.ConnectAsync(smtpServer, smtpPort, socketOption);
                await client.AuthenticateAsync(username.Trim(), cleanPassword);
                await client.SendAsync(message);
                await client.DisconnectAsync(true);

                _logger.LogInformation("Real Email successfully sent via MailKit ({Server}:{Port}) to {ToEmail} with Subject: '{Subject}'", 
                    smtpServer, smtpPort, toEmail, subject);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send email via MailKit to {ToEmail}. Error: {Message}", toEmail, ex.Message);
                throw;
            }
        }

        public async Task SendRegistrationOtpAsync(string toEmail, string recipientName, string code, string userType)
        {
            var subject = "SHMS - Email Verification Code";
            var displayName = string.IsNullOrWhiteSpace(recipientName) ? "User" : recipientName;
            var roleLabel = userType == "ServiceProvider" ? "Service Provider" : "Customer";

            var bodyContent = $@"
                <h2 style=""color: #0F172A; font-size: 19px; font-weight: 700; margin: 0 0 16px 0;"">Hello {displayName},</h2>
                <p style=""font-size: 14px; line-height: 1.6; color: #475569; margin: 0 0 16px 0;"">
                  Thank you for signing up with SHMS. To verify that <strong style=""color: #1E3A8A; font-weight: 700; background-color: #EFF6FF; border: 1px solid #DBEAFE; padding: 2px 8px; border-radius: 6px; display: inline-block; text-decoration: none;"">{toEmail}</strong> belongs to you, please enter the following 6-digit confirmation code:
                </p>

                <div style=""text-align: center; margin: 28px 0;"">
                  <div style=""display: inline-block; background-color: #F8FAFC; border: 2px solid #2563EB; padding: 18px 36px; border-radius: 12px; box-shadow: 0 4px 12px rgba(37, 99, 235, 0.08);"">
                    <div style=""font-size: 11px; font-weight: 700; text-transform: uppercase; letter-spacing: 1.5px; color: #2563EB; margin-bottom: 6px;"">
                      CONFIRMATION CODE
                    </div>
                    <div style=""font-size: 36px; font-weight: 800; letter-spacing: 10px; color: #1E3A8A; font-family: 'Courier New', Courier, monospace;"">
                      {code}
                    </div>
                  </div>
                </div>

                <div style=""background-color: #EFF6FF; border: 1px solid #DBEAFE; padding: 14px 18px; border-radius: 8px; margin-top: 24px; font-size: 13px; color: #1E40AF; line-height: 1.5;"">
                  ⏱️ <strong>Security Notice:</strong> This verification code expires in <strong>15 minutes</strong>. If you did not create an account on SHMS, please safely disregard this email.
                </div>";

            var html = BuildModernEmailTemplate(
                headerBadge: $"{roleLabel.ToUpper()} VERIFICATION",
                headerTitle: "Smart Home Maintenance Services",
                headerSubtitle: $"Email Verification for {roleLabel} Account",
                bodyContent: bodyContent
            );

            await SendEmailAsync(toEmail, subject, html);
        }

        public async Task SendPasswordResetCodeAsync(string toEmail, string recipientName, string code)
        {
            var subject = "SHMS - Password Reset Verification Code";
            var displayName = string.IsNullOrWhiteSpace(recipientName) ? "Valued User" : recipientName;

            var bodyContent = $@"
                <h2 style=""color: #0F172A; font-size: 19px; font-weight: 700; margin: 0 0 16px 0;"">Hello {displayName},</h2>
                <p style=""font-size: 14px; line-height: 1.6; color: #475569; margin: 0 0 12px 0;"">
                  We received a request to reset the password for your SHMS account registered with <strong style=""color: #1E3A8A; font-weight: 700; background-color: #EFF6FF; border: 1px solid #DBEAFE; padding: 2px 8px; border-radius: 6px; display: inline-block; text-decoration: none;"">{toEmail}</strong>.
                </p>
                <p style=""font-size: 14px; line-height: 1.6; color: #475569; margin: 0 0 20px 0;"">
                  Please enter the following 6-digit verification code to complete your password reset:
                </p>

                <div style=""text-align: center; margin: 28px 0;"">
                  <div style=""display: inline-block; background-color: #F8FAFC; border: 2px solid #2563EB; padding: 18px 36px; border-radius: 12px; box-shadow: 0 4px 12px rgba(37, 99, 235, 0.08);"">
                    <div style=""font-size: 11px; font-weight: 700; text-transform: uppercase; letter-spacing: 1.5px; color: #2563EB; margin-bottom: 6px;"">
                      RESET CODE
                    </div>
                    <div style=""font-size: 36px; font-weight: 800; letter-spacing: 10px; color: #1E3A8A; font-family: 'Courier New', Courier, monospace;"">
                      {code}
                    </div>
                  </div>
                </div>

                <div style=""background-color: #EFF6FF; border: 1px solid #DBEAFE; padding: 14px 18px; border-radius: 8px; margin-top: 24px; font-size: 13px; color: #1E40AF; line-height: 1.5;"">
                  ⏱️ <strong>Security Notice:</strong> This code is valid for <strong>15 minutes</strong>. If you did not request a password reset, please ignore this email.
                </div>";

            var html = BuildModernEmailTemplate(
                headerBadge: "ACCOUNT SECURITY",
                headerTitle: "Smart Home Maintenance Services",
                headerSubtitle: "Secure Account Recovery",
                bodyContent: bodyContent
            );

            await SendEmailAsync(toEmail, subject, html);
        }

        public async Task SendWelcomeEmailAsync(string toEmail, string recipientName, string userType)
        {
            var isSp = string.Equals(userType, "ServiceProvider", StringComparison.OrdinalIgnoreCase);
            var subject = isSp
                ? "Your Service Provider Account Has Been Approved - SHMS"
                : "Welcome to Smart Home Maintenance Service (SHMS)";
            var displayName = string.IsNullOrWhiteSpace(recipientName) ? "User" : recipientName;
            var headerBadge = isSp ? "ACCOUNT APPROVED" : "WELCOME TO SHMS";
            var headerSubtitle = isSp ? "Official Provider Approval" : "Welcome to the Platform";

            var messageContent = isSp
                ? @"<p style=""font-size: 14px; line-height: 1.6; color: #475569; margin: 0 0 12px 0;"">
                      Congratulations! Your registration has been officially approved by the Administration team as a <strong>Service Provider</strong>.
                    </p>
                    <p style=""font-size: 14px; line-height: 1.6; color: #475569; margin: 0 0 20px 0;"">
                      You can now log in to the SHMS Provider Portal, manage your services, and receive bookings from verified customers.
                    </p>
                    <div style=""background-color: #F0FDF4; border: 1px solid #DCFCE7; padding: 14px 18px; border-radius: 8px; margin: 20px 0; font-size: 13px; color: #166534; line-height: 1.5;"">
                      ✓ <strong>Status: Active & Approved.</strong> Your profile is now visible to customers in your service area.
                    </div>"
                : @"<p style=""font-size: 14px; line-height: 1.6; color: #475569; margin: 0 0 12px 0;"">
                      Your account has been successfully created and verified as a <strong>Customer</strong>.
                    </p>
                    <p style=""font-size: 14px; line-height: 1.6; color: #475569; margin: 0 0 20px 0;"">
                      You can now log in, browse maintenance services, and connect with verified professionals anytime.
                    </p>
                    <div style=""background-color: #F0FDF4; border: 1px solid #DCFCE7; padding: 14px 18px; border-radius: 8px; margin: 20px 0; font-size: 13px; color: #166534; line-height: 1.5;"">
                      ✓ <strong>Status: Verified.</strong> You're all set to book top-rated home maintenance experts.
                    </div>";

            var bodyContent = $@"
                <h2 style=""color: #0F172A; font-size: 19px; font-weight: 700; margin: 0 0 16px 0;"">Welcome, {displayName}!</h2>
                {messageContent}
                <div style=""text-align: center; margin: 28px 0 12px 0;"">
                  <a href=""http://localhost:4200/login"" style=""background-color: #2563EB; color: #ffffff; font-weight: 600; font-size: 14px; padding: 13px 32px; border-radius: 8px; text-decoration: none; display: inline-block; box-shadow: 0 4px 10px rgba(37, 99, 235, 0.25);"">
                    Sign In to Your Account &rarr;
                  </a>
                </div>";

            var html = BuildModernEmailTemplate(
                headerBadge: headerBadge,
                headerTitle: "Smart Home Maintenance Services",
                headerSubtitle: headerSubtitle,
                bodyContent: bodyContent
            );

            await SendEmailAsync(toEmail, subject, html);
        }

        public async Task SendPasswordChangedNotificationAsync(string toEmail, string recipientName)
        {
            var subject = "SHMS - Password Changed Successfully";
            var displayName = string.IsNullOrWhiteSpace(recipientName) ? "User" : recipientName;

            var bodyContent = $@"
                <h2 style=""color: #0F172A; font-size: 19px; font-weight: 700; margin: 0 0 16px 0;"">Hello {displayName},</h2>
                <p style=""font-size: 14px; line-height: 1.6; color: #475569; margin: 0 0 12px 0;"">
                  The password for your SHMS account (<strong style=""color: #1E3A8A; font-weight: 700; background-color: #EFF6FF; border: 1px solid #DBEAFE; padding: 2px 8px; border-radius: 6px; display: inline-block; text-decoration: none;"">{toEmail}</strong>) was recently changed.
                </p>
                <div style=""background-color: #FEF2F2; border: 1px solid #FEE2E2; padding: 14px 18px; border-radius: 8px; margin: 20px 0; font-size: 13px; color: #991B1B; line-height: 1.5;"">
                  ⚠️ <strong>Security Notice:</strong> If you made this change, no further action is required. If you did not make this change, please contact administration or reset your password immediately.
                </div>";

            var html = BuildModernEmailTemplate(
                headerBadge: "SECURITY NOTIFICATION",
                headerTitle: "Smart Home Maintenance Services",
                headerSubtitle: "Account Security Update",
                bodyContent: bodyContent
            );

            await SendEmailAsync(toEmail, subject, html);
        }

        public async Task SendAccountStatusNotificationAsync(string toEmail, string recipientName, bool isActive)
        {
            var displayName = string.IsNullOrWhiteSpace(recipientName) ? "User" : recipientName;
            var subject = isActive
                ? "Your SHMS Account Has Been Reactivated"
                : "Important Notice: Your SHMS Account Has Been Suspended";

            var headerBadge = isActive ? "ACCOUNT REACTIVATED" : "ACCOUNT SUSPENDED";
            var headerSubtitle = isActive ? "Account Status Restored" : "Account Status Notification";

            var statusBox = isActive
                ? @"<div style=""background-color: #F0FDF4; border: 1px solid #DCFCE7; padding: 14px 18px; border-radius: 8px; margin: 20px 0; font-size: 13px; color: #166534; line-height: 1.5;"">
                      ✓ <strong>Status: Active.</strong> Your account access has been restored by the Administration team. You may now sign in normally.
                    </div>
                    <div style=""text-align: center; margin: 28px 0 12px 0;"">
                      <a href=""http://localhost:4200/login"" style=""background-color: #2563EB; color: #ffffff; font-weight: 600; font-size: 14px; padding: 13px 32px; border-radius: 8px; text-decoration: none; display: inline-block; box-shadow: 0 4px 10px rgba(37, 99, 235, 0.25);"">
                        Sign In to Your Account &rarr;
                      </a>
                    </div>"
                : @"<div style=""background-color: #FEF2F2; border: 1px solid #FEE2E2; padding: 14px 18px; border-radius: 8px; margin: 20px 0; font-size: 13px; color: #991B1B; line-height: 1.5;"">
                      ⚠️ <strong>Status: Blocked / Suspended.</strong> Your SHMS account has been blocked by the Administration team. Please contact support for further assistance.
                    </div>";

            var bodyContent = $@"
                <h2 style=""color: #0F172A; font-size: 19px; font-weight: 700; margin: 0 0 16px 0;"">Hello {displayName},</h2>
                <p style=""font-size: 14px; line-height: 1.6; color: #475569; margin: 0 0 12px 0;"">
                  We are writing to inform you of a status update on your Smart Home Maintenance Services account (<strong style=""color: #1E3A8A; font-weight: 700; background-color: #EFF6FF; border: 1px solid #DBEAFE; padding: 2px 8px; border-radius: 6px; display: inline-block; text-decoration: none;"">{toEmail}</strong>).
                </p>
                {statusBox}";

            var html = BuildModernEmailTemplate(
                headerBadge: headerBadge,
                headerTitle: "Smart Home Maintenance Services",
                headerSubtitle: headerSubtitle,
                bodyContent: bodyContent
            );

            await SendEmailAsync(toEmail, subject, html);
        }

        public async Task SendProviderRejectedNotificationAsync(string toEmail, string recipientName)
        {
            var displayName = string.IsNullOrWhiteSpace(recipientName) ? "Provider" : recipientName;
            var subject = "Update on Your Service Provider Application - SHMS";

            var bodyContent = $@"
                <h2 style=""color: #0F172A; font-size: 19px; font-weight: 700; margin: 0 0 16px 0;"">Hello {displayName},</h2>
                <p style=""font-size: 14px; line-height: 1.6; color: #475569; margin: 0 0 12px 0;"">
                  Thank you for applying to join Smart Home Maintenance Services as a Service Provider.
                </p>
                <div style=""background-color: #FEF2F2; border: 1px solid #FEE2E2; padding: 14px 18px; border-radius: 8px; margin: 20px 0; font-size: 13px; color: #991B1B; line-height: 1.5;"">
                  ⚠️ <strong>Application Status: Not Approved.</strong> Your provider application was not approved at this time or approval has been revoked by the Administration team. Please contact support for details.
                </div>";

            var html = BuildModernEmailTemplate(
                headerBadge: "APPLICATION UPDATE",
                headerTitle: "Smart Home Maintenance Services",
                headerSubtitle: "Service Provider Review",
                bodyContent: bodyContent
            );

            await SendEmailAsync(toEmail, subject, html);
        }

        public async Task SendNotificationEmailAsync(
            string toEmail,
            string recipientName,
            string subject,
            string badge,
            string subtitle,
            string title,
            string messageBody,
            string? statusBanner = null,
            bool isWarning = false)
        {
            var displayName = string.IsNullOrWhiteSpace(recipientName) ? "User" : recipientName;
            var encodedMessage = System.Net.WebUtility.HtmlEncode(messageBody).Replace("\n", "<br/>");

            var bannerHtml = string.Empty;
            if (!string.IsNullOrWhiteSpace(statusBanner))
            {
                var bg = isWarning ? "#FEF2F2" : "#EFF6FF";
                var border = isWarning ? "#FEE2E2" : "#DBEAFE";
                var textCol = isWarning ? "#991B1B" : "#1E40AF";
                var encodedBanner = System.Net.WebUtility.HtmlEncode(statusBanner);
                bannerHtml = $@"
                <div style=""background-color: {bg}; border: 1px solid {border}; padding: 14px 18px; border-radius: 8px; margin: 20px 0; font-size: 13px; color: {textCol}; line-height: 1.5;"">
                  {encodedBanner}
                </div>";
            }

            var bodyContent = $@"
                <h2 style=""color: #0F172A; font-size: 19px; font-weight: 700; margin: 0 0 16px 0;"">Hello {displayName},</h2>
                <p style=""font-size: 15px; font-weight: 600; color: #1E3A8A; margin: 0 0 10px 0;"">{System.Net.WebUtility.HtmlEncode(title)}</p>
                <p style=""font-size: 14px; line-height: 1.6; color: #475569; margin: 0 0 16px 0;"">{encodedMessage}</p>
                {bannerHtml}
                <div style=""text-align: center; margin: 28px 0 12px 0;"">
                  <a href=""http://localhost:4200/login"" style=""background-color: #2563EB; color: #ffffff; font-weight: 600; font-size: 14px; padding: 13px 32px; border-radius: 8px; text-decoration: none; display: inline-block; box-shadow: 0 4px 10px rgba(37, 99, 235, 0.25);"">
                    Open SHMS Portal &rarr;
                  </a>
                </div>";

            var html = BuildModernEmailTemplate(
                headerBadge: badge,
                headerTitle: "Smart Home Maintenance Services",
                headerSubtitle: subtitle,
                bodyContent: bodyContent
            );

            await SendEmailAsync(toEmail, subject, html);
        }

        private static string BuildModernEmailTemplate(string headerBadge, string headerTitle, string headerSubtitle, string bodyContent)
        {
            return $@"
<!DOCTYPE html>
<html lang=""en"">
<head>
  <meta charset=""utf-8"">
  <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"">
  <title>{headerTitle}</title>
</head>
<body style=""font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, Helvetica, Arial, sans-serif; background-color: #F1F5F9; margin: 0; padding: 24px 12px; color: #1E293B;"">
  <table width=""100%"" border=""0"" cellspacing=""0"" cellpadding=""0"" align=""center"">
    <tr>
      <td align=""center"">
        <!-- Main Card Container -->
        <table width=""100%"" border=""0"" cellspacing=""0"" cellpadding=""0"" style=""max-width: 600px; margin: 0 auto; background-color: #FFFFFF; border-radius: 14px; overflow: hidden; box-shadow: 0 10px 25px -5px rgba(0, 0, 0, 0.08), 0 8px 10px -6px rgba(0, 0, 0, 0.04); border: 1px solid #E2E8F0;"">
          
          <!-- Brand Header -->
          <tr>
            <td style=""background: linear-gradient(135deg, #1E3A8A 0%, #2563EB 100%); padding: 32px 28px; text-align: center;"">
              <table width=""100%"" border=""0"" cellspacing=""0"" cellpadding=""0"">
                <tr>
                  <td align=""center"">
                    <!-- Pill Badge -->
                    <div style=""display: inline-block; background-color: rgba(255, 255, 255, 0.16); border: 1px solid rgba(255, 255, 255, 0.28); color: #FFFFFF; padding: 4px 14px; border-radius: 20px; font-size: 11px; font-weight: 700; letter-spacing: 1.2px; text-transform: uppercase; margin-bottom: 10px;"">
                      {headerBadge}
                    </div>
                    <!-- Brand Title -->
                    <h1 style=""color: #FFFFFF; margin: 0 0 6px 0; font-size: 21px; font-weight: 700; letter-spacing: 0.4px;"">
                      {headerTitle}
                    </h1>
                    <!-- Subtitle -->
                    <p style=""color: #BFDBFE; margin: 0; font-size: 13px; font-weight: 400;"">
                      {headerSubtitle}
                    </p>
                  </td>
                </tr>
              </table>
            </td>
          </tr>

          <!-- Body Content Area -->
          <tr>
            <td style=""padding: 34px 30px; background-color: #FFFFFF;"">
              {bodyContent}
            </td>
          </tr>

          <!-- Footer Area -->
          <tr>
            <td style=""background-color: #F8FAFC; padding: 22px 28px; text-align: center; border-top: 1px solid #E2E8F0;"">
              <p style=""margin: 0 0 4px 0; font-size: 12px; color: #64748B; font-weight: 500;"">
                Sent securely from <a href=""mailto:ibilalahmad.dev@gmail.com"" style=""color: #2563EB; text-decoration: none;"">ibilalahmad.dev@gmail.com</a>
              </p>
              <p style=""margin: 0; font-size: 11px; color: #94A3B8;"">
                Smart Home Maintenance Service (SHMS) • All rights reserved
              </p>
            </td>
          </tr>

        </table>
      </td>
    </tr>
  </table>
</body>
</html>";
        }
    }
}
