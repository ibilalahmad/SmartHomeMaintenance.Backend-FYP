using SmartHomeMaintenance.Application.Common.Interfaces;
using System;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Mail;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace SmartHomeMaintenance.Infrastructure.Services
{
    public class EmailVerificationService : IEmailVerificationService
    {
        private readonly ConcurrentDictionary<string, (string Code, DateTime Expiry)> _otpStore = new(StringComparer.OrdinalIgnoreCase);

        // Known legitimate, trusted email providers (guaranteed valid domains, instant validation)
        private static readonly HashSet<string> KnownTrustedDomains = new(StringComparer.OrdinalIgnoreCase)
        {
            "gmail.com", "googlemail.com", "yahoo.com", "ymail.com", "rocketmail.com",
            "outlook.com", "hotmail.com", "live.com", "msn.com",
            "icloud.com", "me.com", "mac.com",
            "proton.me", "protonmail.com",
            "zoho.com", "zohomail.com",
            "aol.com", "aim.com",
            "gmx.com", "gmx.net", "mail.com",
            "yandex.com", "yandex.ru"
        };

        // Disallow obvious bogus/test domains that spammers or tests use
        private static readonly HashSet<string> BlockedDomains = new(StringComparer.OrdinalIgnoreCase)
        {
            "example.com", "fake.com", "test.com", "sample.com", "tempmail.com", "dispostable.com", 
            "mailinator.com", "throwawaymail.com", "yopmail.com", "10minutemail.com", "guerrillamail.com",
            "trashmail.com", "sharklasers.com", "getairmail.com"
        };

        public async Task<bool> IsRealEmailDomainAsync(string email)
        {
            if (string.IsNullOrWhiteSpace(email))
                return false;

            email = email.Trim().ToLowerInvariant();

            // 1. Basic syntax and format check
            if (!Regex.IsMatch(email, @"^[^@\s]+@[^@\s]+\.[^@\s]+$"))
                return false;

            try
            {
                var mailAddress = new MailAddress(email);
                var domain = mailAddress.Host.Trim().ToLowerInvariant();

                if (string.IsNullOrWhiteSpace(domain) || !domain.Contains('.'))
                    return false;

                // 2. Reject blocked / disposable test domains
                if (BlockedDomains.Contains(domain))
                    return false;

                // 3. Automatically trust known standard email providers (zero network/DNS failure risk)
                if (KnownTrustedDomains.Contains(domain))
                    return true;

                // Also allow common subdomains/regional variants (e.g., yahoo.co.uk)
                if (domain.EndsWith(".gmail.com") || domain.EndsWith(".yahoo.com") || domain.EndsWith(".outlook.com"))
                    return true;

                // 4. DNS Host resolution with timeout to check if custom/corporate domain exists
                using var cts = new System.Threading.CancellationTokenSource(TimeSpan.FromSeconds(3));
                var addresses = await Dns.GetHostAddressesAsync(domain, cts.Token);
                if (addresses != null && addresses.Length > 0)
                    return true;

                return false;
            }
            catch (SocketException ex) when (ex.SocketErrorCode == SocketError.HostNotFound || ex.SocketErrorCode == SocketError.NoData)
            {
                // DNS lookup definitively confirmed host does not exist
                return false;
            }
            catch
            {
                // In case of transient DNS resolver glitches or timeout, check if the domain has a valid structure and TLD
                var parts = email.Split('@');
                if (parts.Length == 2)
                {
                    var host = parts[1].Trim();
                    var lastDot = host.LastIndexOf('.');
                    if (lastDot > 0 && lastDot < host.Length - 1)
                    {
                        var tld = host.Substring(lastDot + 1);
                        if (tld.Length >= 2 && Regex.IsMatch(tld, @"^[a-zA-Z]+$"))
                        {
                            return true; // Allow through to OTP verification step
                        }
                    }
                }
                return false;
            }
        }

        public string GenerateAndStoreOtp(string email)
        {
            var code = RandomNumberGenerator.GetInt32(100000, 999999).ToString();
            var expiry = DateTime.UtcNow.AddMinutes(15);
            _otpStore[email.Trim()] = (code, expiry);
            return code;
        }

        public bool ValidateOtp(string email, string code)
        {
            if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(code))
                return false;

            if (_otpStore.TryGetValue(email.Trim(), out var entry))
            {
                if (DateTime.UtcNow <= entry.Expiry && entry.Code == code.Trim())
                {
                    return true;
                }
            }

            return false;
        }

        public void InvalidateOtp(string email)
        {
            if (!string.IsNullOrWhiteSpace(email))
            {
                _otpStore.TryRemove(email.Trim(), out _);
            }
        }
    }
}
