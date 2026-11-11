using SmartHomeMaintenance.Application.Common.Interfaces;
using System;
using System.Collections.Concurrent;
using System.Security.Cryptography;

namespace SmartHomeMaintenance.Infrastructure.Services
{
    public class PasswordResetTokenService : IPasswordResetTokenService
    {
        private readonly ConcurrentDictionary<string, (string Code, DateTime Expiry)> _store = new(StringComparer.OrdinalIgnoreCase);

        public string GenerateAndStoreCode(string email)
        {
            var code = RandomNumberGenerator.GetInt32(100000, 999999).ToString();
            var expiry = DateTime.UtcNow.AddMinutes(15);
            _store[email.Trim()] = (code, expiry);
            return code;
        }

        public bool ValidateCode(string email, string code)
        {
            if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(code))
                return false;

            if (_store.TryGetValue(email.Trim(), out var entry))
            {
                if (DateTime.UtcNow <= entry.Expiry && entry.Code == code.Trim())
                {
                    return true;
                }
            }

            return false;
        }

        public void InvalidateCode(string email)
        {
            if (!string.IsNullOrWhiteSpace(email))
            {
                _store.TryRemove(email.Trim(), out _);
            }
        }
    }
}
