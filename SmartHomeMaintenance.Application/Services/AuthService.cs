using Microsoft.EntityFrameworkCore;
using SmartHomeMaintenance.Application.Common.Interfaces;
using SmartHomeMaintenance.Application.DTOs.Auth;
using SmartHomeMaintenance.Domain.Entities;
using SmartHomeMaintenance.Domain.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace SmartHomeMaintenance.Application.Services
{
    public class AuthService : IAuthService
    {
        private readonly IApplicationDbContext _context;
        private readonly IJwtTokenGenerator _jwtTokenGenerator;
        private readonly IEmailService _emailService;
        private readonly IPasswordResetTokenService _tokenService;
        private readonly IEmailVerificationService _emailVerificationService;

        public AuthService(
            IApplicationDbContext context, 
            IJwtTokenGenerator jwtTokenGenerator,
            IEmailService emailService,
            IPasswordResetTokenService tokenService,
            IEmailVerificationService emailVerificationService)
        {
            _context = context;
            _jwtTokenGenerator = jwtTokenGenerator;
            _emailService = emailService;
            _tokenService = tokenService;
            _emailVerificationService = emailVerificationService;
        }

        public async Task<LoginResponseDto> LoginAsync(LoginRequestDto request)
        {
            var user = await _context.Users
                .Include(u => u.Customer)
                .Include(u => u.ServiceProvider)
                .Include(u => u.Admin)
                .FirstOrDefaultAsync(u => u.Email.ToLower() == request.Email.ToLower().Trim());

            if (user != null && user.LockoutEnd.HasValue && user.LockoutEnd.Value > DateTime.UtcNow)
            {
                var minsLeft = Math.Max(1, (int)Math.Ceiling((user.LockoutEnd.Value - DateTime.UtcNow).TotalMinutes));
                throw new UnauthorizedAccessException($"Account temporarily locked for {minsLeft} minute(s) due to multiple failed login attempts. Please try again later.");
            }

            if (user == null || !BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
            {
                if (user != null)
                {
                    user.FailedLoginAttempts += 1;
                    if (user.FailedLoginAttempts >= 3)
                    {
                        user.LockoutEnd = DateTime.UtcNow.AddMinutes(15);
                        user.FailedLoginAttempts = 0;
                        await _context.SaveChangesAsync();
                        throw new UnauthorizedAccessException("Account temporarily locked for 15 minutes due to 3 failed login attempts.");
                    }
                    await _context.SaveChangesAsync();
                }
                throw new UnauthorizedAccessException("Invalid email or password. Please try again.");
            }

            // Successful credential check -> reset failed attempts & lockout
            if (user.FailedLoginAttempts > 0 || user.LockoutEnd.HasValue)
            {
                user.FailedLoginAttempts = 0;
                user.LockoutEnd = null;
            }

            if (!user.IsActive)
            {
                throw new InvalidOperationException("Your account has been blocked. Please contact admin for more information.");
            }

            // Reactivate temporarily deactivated account on explicit sign-in
            if (user.IsTemporarilyDeactivated)
            {
                user.IsTemporarilyDeactivated = false;
                if (user.ServiceProvider != null)
                {
                    user.ServiceProvider.IsAvailable = true;
                }
            }

            // Enforce email verification for non-admin accounts
            if (!user.IsEmailVerified && user.UserType != UserType.Admin)
            {
                // Send fresh OTP so they can easily finish verification
                try
                {
                    var otp = _emailVerificationService.GenerateAndStoreOtp(user.Email);
                    _ = Task.Run(() => _emailService.SendRegistrationOtpAsync(user.Email, user.FullName, otp, user.UserType.ToString()));
                }
                catch { /* Non-blocking */ }

                throw new InvalidOperationException("Your email address is not verified yet. We have sent a 6-digit confirmation code to your email. Please verify your email before logging in.");
            }

            bool? isApproved = null;
            if (user.UserType == UserType.ServiceProvider && user.ServiceProvider != null)
            {
                isApproved = user.ServiceProvider.IsApproved;
                if (!user.ServiceProvider.IsApproved)
                {
                    var reasonSuffix = !string.IsNullOrWhiteSpace(user.ServiceProvider.RejectionReason)
                        ? $" Reason: {user.ServiceProvider.RejectionReason}"
                        : string.Empty;
                    throw new InvalidOperationException($"Your account is not approved yet. Please wait for an Admin to review and approve your account before logging in.{reasonSuffix}");
                }
            }

            if (user.UserType == UserType.Admin && user.Admin != null)
            {
                user.Admin.LastLogin = DateTime.UtcNow;
            }

            await _context.SaveChangesAsync();

            var expiryDays = request.RememberMe ? 30 : 1;
            var token = _jwtTokenGenerator.GenerateToken(user, expiryDays);
            string? adminRole = null;
            if (user.UserType == UserType.Admin)
            {
                adminRole = user.Admin?.Role ?? "SuperAdmin";
            }

            return new LoginResponseDto
            {
                UserId = user.Id,
                FullName = user.FullName,
                Email = user.Email,
                UserType = user.UserType.ToString(),
                AdminRole = adminRole,
                ProfilePic = user.ProfilePic,
                IsActive = user.IsActive,
                IsApproved = isApproved,
                Token = token,
                Message = "Login successful."
            };
        }

        public async Task<RegistrationResponseDto> RegisterCustomerAsync(CustomerRegisterDto request)
        {
            // 1. Verify Real Domain via DNS
            var isValidDomain = await _emailVerificationService.IsRealEmailDomainAsync(request.Email);
            if (!isValidDomain)
            {
                throw new InvalidOperationException("Please provide a valid, active email address with an existing domain (e.g. @gmail.com, @yahoo.com). Fake or non-existent domains are not permitted.");
            }

            var normalizedEmail = request.Email.Trim().ToLower();
            var existingUser = await _context.Users
                .Include(u => u.Customer)
                .FirstOrDefaultAsync(u => u.Email.ToLower() == normalizedEmail);

            if (existingUser != null)
            {
                if (existingUser.IsEmailVerified)
                {
                    throw new InvalidOperationException("This email is already registered and verified. Please log in.");
                }

                // Update unverified record with new details
                existingUser.FullName = request.FullName.Trim();
                existingUser.PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password);
                existingUser.PhoneNumber = request.PhoneNumber?.Trim() ?? string.Empty;
                existingUser.Address = request.Address;
                existingUser.ProfilePic = request.ProfilePic;
                if (existingUser.Customer != null)
                {
                    existingUser.Customer.PreferredPayment = request.PreferredPayment;
                    existingUser.Customer.DefaultAddress = request.Address;
                }
                await _context.SaveChangesAsync();

                var existingOtp = _emailVerificationService.GenerateAndStoreOtp(existingUser.Email);
                await _emailService.SendRegistrationOtpAsync(existingUser.Email, existingUser.FullName, existingOtp, "Customer");

                return new RegistrationResponseDto
                {
                    RequireOtp = true,
                    Email = existingUser.Email,
                    UserType = "Customer",
                    UserId = existingUser.Id,
                    Message = "A 6-digit confirmation code has been sent to your email. Please enter it to verify your account."
                };
            }

            var user = new User
            {
                FullName = request.FullName.Trim(),
                Email = request.Email.Trim(),
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password),
                PhoneNumber = request.PhoneNumber?.Trim() ?? string.Empty,
                Address = request.Address,
                UserType = UserType.Customer,
                RegDate = DateTime.UtcNow,
                IsActive = true,
                IsEmailVerified = false,
                ProfilePic = request.ProfilePic,
                Customer = new Customer
                {
                    PreferredPayment = request.PreferredPayment,
                    DefaultAddress = request.Address,
                    LoyaltyPoints = 0,
                    ProfilePic = request.ProfilePic
                }
            };

            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            var otp = _emailVerificationService.GenerateAndStoreOtp(user.Email);
            await _emailService.SendRegistrationOtpAsync(user.Email, user.FullName, otp, "Customer");

            return new RegistrationResponseDto
            {
                RequireOtp = true,
                Email = user.Email,
                UserType = "Customer",
                UserId = user.Id,
                Message = "A 6-digit confirmation code has been sent to your email. Please enter it to verify your account."
            };
        }

        public async Task<RegistrationResponseDto> RegisterServiceProviderAsync(ServiceProviderRegisterDto request)
        {
            // 1. Verify Real Domain via DNS
            var isValidDomain = await _emailVerificationService.IsRealEmailDomainAsync(request.Email);
            if (!isValidDomain)
            {
                throw new InvalidOperationException("Please provide a valid, active email address with an existing domain (e.g. @gmail.com, @yahoo.com). Fake or non-existent domains are not permitted.");
            }

            var normalizedEmail = request.Email.Trim().ToLower();
            var existingUser = await _context.Users
                .Include(u => u.ServiceProvider)
                .FirstOrDefaultAsync(u => u.Email.ToLower() == normalizedEmail);

            if (existingUser != null)
            {
                if (existingUser.IsEmailVerified)
                {
                    throw new InvalidOperationException("This email is already registered and verified. Please log in.");
                }

                // Update unverified record with new details
                existingUser.FullName = request.FullName.Trim();
                existingUser.PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password);
                existingUser.PhoneNumber = request.PhoneNumber?.Trim() ?? string.Empty;
                existingUser.Address = request.Address;
                existingUser.ProfilePic = request.ProfilePic;
                if (existingUser.ServiceProvider != null)
                {
                    existingUser.ServiceProvider.WorkExperience = request.WorkExperience;
                    existingUser.ServiceProvider.Expertise = request.Expertise;
                    existingUser.ServiceProvider.Qualification = request.Qualification;
                    existingUser.ServiceProvider.HourlyRate = request.HourlyRate;
                    existingUser.ServiceProvider.Location = request.Location;
                }
                await _context.SaveChangesAsync();

                var existingOtp = _emailVerificationService.GenerateAndStoreOtp(existingUser.Email);
                await _emailService.SendRegistrationOtpAsync(existingUser.Email, existingUser.FullName, existingOtp, "ServiceProvider");

                return new RegistrationResponseDto
                {
                    RequireOtp = true,
                    Email = existingUser.Email,
                    UserType = "ServiceProvider",
                    UserId = existingUser.Id,
                    Message = "A 6-digit confirmation code has been sent to your email. Please enter it to verify your account."
                };
            }

            var user = new User
            {
                FullName = request.FullName.Trim(),
                Email = request.Email.Trim(),
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password),
                PhoneNumber = request.PhoneNumber?.Trim() ?? string.Empty,
                Address = request.Address,
                UserType = UserType.ServiceProvider,
                RegDate = DateTime.UtcNow,
                IsActive = true,
                IsEmailVerified = false,
                ProfilePic = request.ProfilePic,
                ServiceProvider = new ServiceProvider
                {
                    WorkExperience = request.WorkExperience,
                    Expertise = request.Expertise,
                    Qualification = request.Qualification,
                    HourlyRate = request.HourlyRate,
                    Location = request.Location,
                    Latitude = request.Latitude,
                    Longitude = request.Longitude,
                    IsApproved = false, // Pending Admin Approval
                    ProfilePic = request.ProfilePic
                }
            };

            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            var otp = _emailVerificationService.GenerateAndStoreOtp(user.Email);
            await _emailService.SendRegistrationOtpAsync(user.Email, user.FullName, otp, "ServiceProvider");

            return new RegistrationResponseDto
            {
                RequireOtp = true,
                Email = user.Email,
                UserType = "ServiceProvider",
                UserId = user.Id,
                Message = "A 6-digit confirmation code has been sent to your email. Please enter it to verify your account."
            };
        }

        public async Task<LoginResponseDto> VerifyRegistrationOtpAsync(VerifyRegistrationOtpRequestDto request)
        {
            if (!_emailVerificationService.ValidateOtp(request.Email, request.Code))
            {
                throw new InvalidOperationException("Invalid or expired verification code. Please check your inbox or click Resend Code.");
            }

            var normalizedEmail = request.Email.Trim().ToLower();
            var user = await _context.Users
                .Include(u => u.Customer)
                .Include(u => u.ServiceProvider)
                .FirstOrDefaultAsync(u => u.Email.ToLower() == normalizedEmail);

            if (user == null)
            {
                throw new KeyNotFoundException("User account not found.");
            }

            user.IsEmailVerified = true;
            await _context.SaveChangesAsync();

            _emailVerificationService.InvalidateOtp(user.Email);

            // Send Welcome Email in background for Customers only.
            // Service Providers must wait for Admin approval before receiving confirmation & login instructions.
            if (user.UserType == UserType.Customer)
            {
                try
                {
                    _ = Task.Run(() => _emailService.SendWelcomeEmailAsync(user.Email, user.FullName, "Customer"));
                }
                catch { /* Non-blocking */ }
            }
            else if (user.UserType == UserType.ServiceProvider)
            {
                // FR-ADMIN-47 / UC-SP-01: Notify Admins of new pending Service Provider registration
                try
                {
                    var adminUsers = await _context.Users
                        .Where(u => u.UserType == UserType.Admin && u.IsActive)
                        .ToListAsync();
                    foreach (var admin in adminUsers)
                    {
                        _context.Notifications.Add(new AppNotification
                        {
                            UserId = admin.Id,
                            Title = "New Service Provider Registration",
                            Message = $"{user.FullName} ({user.Email}) applied as a Service Provider and is awaiting your approval.",
                            Type = "info",
                            LinkUrl = "/admin/providers",
                            IsRead = false,
                            CreatedAt = DateTime.UtcNow
                        });
                    }
                    await _context.SaveChangesAsync();
                }
                catch { /* Non-blocking */ }
            }

            var token = _jwtTokenGenerator.GenerateToken(user);
            bool? isApproved = user.ServiceProvider?.IsApproved;

            var message = user.UserType == UserType.ServiceProvider
                ? "Email verified successfully! Your account has been submitted for Admin approval."
                : "Email verified successfully! Welcome to SHMS.";

            return new LoginResponseDto
            {
                UserId = user.Id,
                FullName = user.FullName,
                Email = user.Email,
                UserType = user.UserType.ToString(),
                ProfilePic = user.ProfilePic,
                IsActive = user.IsActive,
                IsApproved = isApproved,
                Token = user.UserType == UserType.Customer ? token : string.Empty,
                Message = message
            };
        }

        public async Task<bool> ResendRegistrationOtpAsync(string email)
        {
            if (string.IsNullOrWhiteSpace(email))
                throw new InvalidOperationException("Email is required.");

            var normalizedEmail = email.Trim().ToLower();
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Email.ToLower() == normalizedEmail);

            if (user == null)
            {
                throw new KeyNotFoundException("Account not found.");
            }

            if (user.IsEmailVerified)
            {
                throw new InvalidOperationException("Account email is already verified. Please proceed to login.");
            }

            var otp = _emailVerificationService.GenerateAndStoreOtp(user.Email);
            await _emailService.SendRegistrationOtpAsync(user.Email, user.FullName, otp, user.UserType.ToString());
            return true;
        }

        public async Task<bool> ForgotPasswordAsync(string email)
        {
            if (string.IsNullOrWhiteSpace(email))
                return true;

            var user = await _context.Users.FirstOrDefaultAsync(u => u.Email.ToLower() == email.Trim().ToLower());
            if (user == null)
            {
                // Return true silently for privacy, so attackers cannot enumerate valid user emails
                return true;
            }

            var code = _tokenService.GenerateAndStoreCode(user.Email);
            await _emailService.SendPasswordResetCodeAsync(user.Email, user.FullName, code);
            return true;
        }

        public Task<bool> VerifyResetCodeAsync(string email, string code)
        {
            var isValid = _tokenService.ValidateCode(email, code);
            return Task.FromResult(isValid);
        }

        public async Task<bool> ResetPasswordWithCodeAsync(string email, string code, string newPassword)
        {
            if (!_tokenService.ValidateCode(email, code))
            {
                throw new InvalidOperationException("Invalid or expired verification code. Please request a new one.");
            }

            var user = await _context.Users.FirstOrDefaultAsync(u => u.Email.ToLower() == email.Trim().ToLower());
            if (user == null)
            {
                throw new KeyNotFoundException("User not found.");
            }

            user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(newPassword);
            await _context.SaveChangesAsync();

            _tokenService.InvalidateCode(email);

            try
            {
                await _emailService.SendPasswordChangedNotificationAsync(user.Email, user.FullName);
            }
            catch { /* Non-blocking */ }

            return true;
        }

        public async Task<bool> AdminResetPasswordAsync(int userId, string newPassword)
        {
            if (string.IsNullOrWhiteSpace(newPassword) || newPassword.Length < 8 ||
                !newPassword.Any(char.IsUpper) || !newPassword.Any(char.IsLower) ||
                !newPassword.Any(char.IsDigit) || !newPassword.Any(ch => "!@#$%^&*(),.?\":{}|<>".Contains(ch)))
            {
                throw new InvalidOperationException("Password must be at least 8 characters and contain at least one uppercase letter, one lowercase letter, one number, and one special character (!@#$%^&*).");
            }

            var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId);
            if (user == null)
            {
                throw new KeyNotFoundException("User not found.");
            }

            user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(newPassword);
            await _context.SaveChangesAsync();

            try
            {
                _ = Task.Run(() => _emailService.SendPasswordChangedNotificationAsync(user.Email, user.FullName));
            }
            catch { /* Non-blocking */ }

            return true;
        }

        public async Task<bool> ChangePasswordAsync(int userId, string oldPassword, string newPassword)
        {
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId);
            if (user == null)
            {
                throw new KeyNotFoundException("User not found.");
            }

            if (!BCrypt.Net.BCrypt.Verify(oldPassword, user.PasswordHash))
            {
                throw new UnauthorizedAccessException("Incorrect current password.");
            }

            user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(newPassword);
            await _context.SaveChangesAsync();

            try
            {
                _ = Task.Run(() => _emailService.SendPasswordChangedNotificationAsync(user.Email, user.FullName));
            }
            catch { /* Non-blocking */ }

            return true;
        }
    }
}
