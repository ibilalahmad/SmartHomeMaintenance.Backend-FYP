using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmartHomeMaintenance.Application.Common.Interfaces;
using SmartHomeMaintenance.Application.DTOs.Admin;
using SmartHomeMaintenance.Application.DTOs.Auth;
using SmartHomeMaintenance.Domain.Entities;
using SmartHomeMaintenance.Domain.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace SmartHomeMaintenance.API.Controllers
{
    [Authorize(Roles = "Admin")]
    [ApiController]
    [Route("api/[controller]")]
    public class AdminController : ControllerBase
    {
        private readonly IUserService _userService;
        private readonly IServiceProviderService _spService;
        private readonly IAuthService _authService;
        private readonly IApplicationDbContext _context;
        private readonly IEmailVerificationService _emailVerificationService;
        private readonly IEmailService _emailService;

        public AdminController(
            IUserService userService, 
            IServiceProviderService spService, 
            IAuthService authService,
            IApplicationDbContext context,
            IEmailVerificationService emailVerificationService,
            IEmailService emailService)
        {
            _userService = userService;
            _spService = spService;
            _authService = authService;
            _context = context;
            _emailVerificationService = emailVerificationService;
            _emailService = emailService;
        }

        [HttpGet("profile")]
        public async Task<IActionResult> GetProfile()
        {
            var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value 
                              ?? User.FindFirst("sub")?.Value;

            User? adminUser = null;
            if (int.TryParse(userIdClaim, out var userId))
            {
                adminUser = await _context.Users
                    .Include(u => u.Admin)
                    .FirstOrDefaultAsync(u => u.Id == userId);
            }

            if (adminUser == null)
            {
                var emailClaim = User.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value ?? User.FindFirst("email")?.Value;
                if (!string.IsNullOrEmpty(emailClaim))
                {
                    adminUser = await _context.Users
                        .Include(u => u.Admin)
                        .FirstOrDefaultAsync(u => u.Email.ToLower() == emailClaim.ToLower());
                }
            }

            if (adminUser == null)
            {
                adminUser = await _context.Users
                    .Include(u => u.Admin)
                    .FirstOrDefaultAsync(u => u.Email == "admin@shms.com" || u.UserType == UserType.Admin);
            }

            if (adminUser == null)
                return NotFound(new { message = "Admin not found." });

            return Ok(new
            {
                adminId = adminUser.Id,
                fullName = adminUser.FullName,
                email = adminUser.Email,
                phoneNumber = adminUser.PhoneNumber,
                address = adminUser.Address,
                profilePic = !string.IsNullOrEmpty(adminUser.ProfilePic) ? adminUser.ProfilePic : adminUser.Admin?.ProfilePic,
                role = adminUser.Admin?.Role ?? "SuperAdmin"
            });
        }

        [HttpPut("profile")]
        public async Task<IActionResult> UpdateProfile([FromBody] UpdateAdminProfileRequest request)
        {
            var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value 
                              ?? User.FindFirst("sub")?.Value;

            User? adminUser = null;
            if (int.TryParse(userIdClaim, out var userId))
            {
                adminUser = await _context.Users
                    .Include(u => u.Admin)
                    .FirstOrDefaultAsync(u => u.Id == userId);
            }

            if (adminUser == null)
            {
                var emailClaim = User.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value ?? User.FindFirst("email")?.Value;
                if (!string.IsNullOrEmpty(emailClaim))
                {
                    adminUser = await _context.Users
                        .Include(u => u.Admin)
                        .FirstOrDefaultAsync(u => u.Email.ToLower() == emailClaim.ToLower());
                }
            }

            if (adminUser == null)
            {
                adminUser = await _context.Users
                    .Include(u => u.Admin)
                    .FirstOrDefaultAsync(u => u.Email == "admin@shms.com" || u.UserType == UserType.Admin);
            }

            if (adminUser == null)
                return NotFound(new { message = "Admin not found." });

            if (!string.IsNullOrWhiteSpace(request.FullName))
                adminUser.FullName = request.FullName;

            if (!string.IsNullOrWhiteSpace(request.Email))
                adminUser.Email = request.Email;

            if (!string.IsNullOrWhiteSpace(request.PhoneNumber))
                adminUser.PhoneNumber = request.PhoneNumber;

            if (!string.IsNullOrWhiteSpace(request.Address))
                adminUser.Address = request.Address;

            if (request.ProfilePic != null)
            {
                var cleanedPic = string.IsNullOrWhiteSpace(request.ProfilePic) ? null : request.ProfilePic.Trim();
                adminUser.ProfilePic = cleanedPic;
                if (adminUser.Admin != null)
                    adminUser.Admin.ProfilePic = cleanedPic;
            }

            await _context.SaveChangesAsync();

            var finalPic = !string.IsNullOrEmpty(adminUser.ProfilePic) ? adminUser.ProfilePic : adminUser.Admin?.ProfilePic;

            return Ok(new
            {
                adminId = adminUser.Id,
                fullName = adminUser.FullName,
                email = adminUser.Email,
                phoneNumber = adminUser.PhoneNumber,
                address = adminUser.Address,
                profilePic = finalPic,
                role = adminUser.Admin?.Role ?? "SuperAdmin",
                message = "Admin profile updated successfully."
            });
        }

        [HttpPost("change-password")]
        public async Task<IActionResult> ChangePassword([FromBody] AdminChangePasswordRequest request)
        {
            var adminUser = await _context.Users
                .FirstOrDefaultAsync(u => u.Email == "admin@shms.com" || u.UserType == UserType.Admin);

            if (adminUser == null)
                return NotFound(new { message = "Admin not found." });

            if (!BCrypt.Net.BCrypt.Verify(request.CurrentPassword, adminUser.PasswordHash))
                return BadRequest(new { message = "Current password is incorrect." });

            adminUser.PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.NewPassword);
            await _context.SaveChangesAsync();
            return Ok(new { message = "Password updated successfully." });
        }

        [HttpGet("customers")]
        public async Task<IActionResult> GetAllCustomers()
        {
            var result = await _userService.GetAllCustomersAsync();
            return Ok(result);
        }

        [HttpPost("customers")]
        public async Task<IActionResult> CreateCustomer([FromBody] CustomerRegisterDto request)
        {
            try
            {
                if (request == null || string.IsNullOrWhiteSpace(request.FullName) || string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Password))
                {
                    return BadRequest(new { message = "Full name, valid email, and secure password (min 8 chars) are required." });
                }

                var isValidDomain = await _emailVerificationService.IsRealEmailDomainAsync(request.Email);
                if (!isValidDomain)
                {
                    return BadRequest(new { message = "Please provide a valid, active email address with an existing domain." });
                }

                var normalizedEmail = request.Email.Trim().ToLower();
                if (await _context.Users.AnyAsync(u => u.Email.ToLower() == normalizedEmail))
                {
                    return BadRequest(new { message = "A user with this email address is already registered." });
                }

                var defaultAddr = request.DefaultAddress ?? request.Address;
                var user = new User
                {
                    FullName = request.FullName.Trim(),
                    Email = request.Email.Trim(),
                    PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password),
                    PhoneNumber = request.PhoneNumber?.Trim() ?? string.Empty,
                    Address = request.Address ?? defaultAddr,
                    UserType = UserType.Customer,
                    RegDate = DateTime.UtcNow,
                    IsActive = true,
                    IsEmailVerified = true,
                    ProfilePic = request.ProfilePic,
                    Customer = new Customer
                    {
                        PreferredPayment = string.IsNullOrWhiteSpace(request.PreferredPayment) ? "Cash" : request.PreferredPayment,
                        DefaultAddress = defaultAddr,
                        ProfilePic = request.ProfilePic
                    }
                };

                _context.Users.Add(user);
                await _context.SaveChangesAsync();

                if (!string.IsNullOrWhiteSpace(user.Email))
                {
                    try
                    {
                        _ = Task.Run(() => _emailService.SendWelcomeEmailAsync(user.Email, user.FullName, "Customer"));
                    }
                    catch { /* Non-blocking */ }
                }

                return Ok(new { success = true, message = "Customer created successfully.", id = user.Id });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Failed to create customer: " + ex.Message });
            }
        }

        [HttpGet("service-providers")]
        public async Task<IActionResult> GetAllServiceProviders()
        {
            var result = await _spService.GetAllServiceProvidersAsync();
            return Ok(result);
        }

        [HttpPost("service-providers")]
        public async Task<IActionResult> CreateServiceProvider([FromBody] ServiceProviderRegisterDto request)
        {
            try
            {
                if (request == null || string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Password))
                {
                    return BadRequest(new { message = "Full name, valid email, and secure password are required." });
                }

                // 1. Validate real email domain
                var isValidDomain = await _emailVerificationService.IsRealEmailDomainAsync(request.Email);
                if (!isValidDomain)
                {
                    return BadRequest(new { message = "Please provide a valid, active email address with an existing domain (e.g. @gmail.com, @yahoo.com). Fake or non-existent domains are not permitted." });
                }

                var normalizedEmail = request.Email.Trim().ToLower();
                var exists = await _context.Users.AnyAsync(u => u.Email.ToLower() == normalizedEmail);
                if (exists)
                {
                    return BadRequest(new { message = "A user with this email address is already registered." });
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
                    IsEmailVerified = true, // Admin-created accounts are directly verified
                    ProfilePic = request.ProfilePic,
                    ServiceProvider = new SmartHomeMaintenance.Domain.Entities.ServiceProvider
                    {
                        WorkExperience = request.WorkExperience,
                        Expertise = request.Expertise,
                        Qualification = request.Qualification,
                        HourlyRate = request.HourlyRate,
                        Location = request.Location,
                        Latitude = request.Latitude,
                        Longitude = request.Longitude,
                        IsApproved = true, // Admin-created accounts are directly approved
                        ProfilePic = request.ProfilePic
                    }
                };

                _context.Users.Add(user);
                await _context.SaveChangesAsync();

                if (!string.IsNullOrWhiteSpace(user.Email))
                {
                    try
                    {
                        _ = Task.Run(() => _emailService.SendWelcomeEmailAsync(user.Email, user.FullName, "ServiceProvider"));
                    }
                    catch { /* Non-blocking */ }
                }

                return Ok(new { success = true, message = "Service provider created and approved successfully." });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Failed to create service provider: " + ex.Message });
            }
        }

        [HttpPut("service-providers/{id}/approve")]
        public async Task<IActionResult> ApproveServiceProvider(int id, [FromQuery] bool isApproved = true, [FromBody] ApproveProviderBody? body = null)
        {
            var result = await _spService.UpdateApprovalStatusAsync(id, isApproved, body?.Reason, body?.RequestMoreInfo ?? false);
            var msg = isApproved
                ? "Service provider approved."
                : (body?.RequestMoreInfo == true ? "Requested additional information from provider." : "Service provider rejected.");
            return Ok(new { success = result, message = msg });
        }

        [HttpPost("service-providers/{id}/message")]
        public async Task<IActionResult> SendMessageToProvider(int id, [FromBody] SendMessageToProviderRequest request)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.Message))
            {
                return BadRequest(new { message = "Message content is required." });
            }

            var provider = await _context.ServiceProviders
                .Include(sp => sp.User)
                .FirstOrDefaultAsync(sp => sp.ProviderId == id);

            if (provider == null || provider.User == null)
            {
                return NotFound(new { message = "Service provider not found." });
            }

            var subject = string.IsNullOrWhiteSpace(request.Subject) ? "Message from SHMS Administration" : request.Subject.Trim();
            var bodyText = request.Message.Trim();

            _context.Notifications.Add(new AppNotification
            {
                UserId = provider.ProviderId,
                Title = subject,
                Message = bodyText,
                Type = "info",
                LinkUrl = "/sp/notifications",
                IsRead = false,
                CreatedAt = DateTime.UtcNow
            });
            await _context.SaveChangesAsync();

            if (!string.IsNullOrWhiteSpace(provider.User.Email))
            {
                try
                {
                    await _emailService.SendNotificationEmailAsync(
                        provider.User.Email,
                        provider.User.FullName,
                        subject,
                        "ADMIN MESSAGE",
                        "Direct Notice from SHMS Admin",
                        subject,
                        bodyText);
                }
                catch { /* Non-blocking */ }
            }

            return Ok(new { success = true, message = "Message sent to service provider." });
        }

        [HttpPut("users/{id}/status")]
        public async Task<IActionResult> UpdateUserStatus(int id, [FromQuery] bool isActive)
        {
            var result = await _userService.UpdateUserStatusAsync(id, isActive);
            return Ok(new { success = result, message = isActive ? "User unblocked successfully." : "User blocked successfully." });
        }

        [HttpDelete("customers/{id}")]
        public async Task<IActionResult> DeleteCustomer(int id)
        {
            var result = await _userService.DeleteCustomerAsync(id);
            return Ok(new { success = result, message = "Customer removed by admin." });
        }

        [HttpDelete("service-providers/{id}")]
        public async Task<IActionResult> DeleteServiceProvider(int id)
        {
            var result = await _spService.DeleteServiceProviderAsync(id);
            return Ok(new { success = result, message = "Service provider removed by admin." });
        }

        [HttpPost("users/{id}/reset-password")]
        public async Task<IActionResult> ResetPassword(int id, [FromBody] ResetPasswordRequest request)
        {
            try
            {
                var result = await _authService.AdminResetPasswordAsync(id, request.NewPassword);
                return Ok(new { success = result, message = "Password reset successfully." });
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new { message = ex.Message });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Failed to reset password.", details = ex.Message });
            }
        }

        #region Admin Accounts Management

        private async Task<(bool isAllowed, string? errorMessage)> EnsureSuperAdminAsync()
        {
            var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value 
                              ?? User.FindFirst("sub")?.Value;

            if (int.TryParse(userIdClaim, out var userId))
            {
                var admin = await _context.Admins.FirstOrDefaultAsync(a => a.AdminId == userId);
                if (admin != null && (admin.Role == "Super Admin" || admin.Role == "SuperAdmin"))
                {
                    return (true, null);
                }
            }

            var emailClaim = User.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value ?? User.FindFirst("email")?.Value;
            if (emailClaim?.ToLower() == "admin@shms.com")
            {
                return (true, null);
            }

            return (false, "Access Denied: Only Super Admin accounts can view or manage administrator accounts.");
        }

        [HttpGet("admins")]
        public async Task<IActionResult> GetAllAdmins()
        {
            try
            {
                var (isAllowed, errorMsg) = await EnsureSuperAdminAsync();
                if (!isAllowed)
                {
                    return StatusCode(403, new { message = errorMsg });
                }

                var adminUsers = await _context.Users
                    .Include(u => u.Admin)
                    .Where(u => u.UserType == UserType.Admin)
                    .OrderByDescending(u => u.RegDate)
                    .Select(u => new AdminDto
                    {
                        AdminId = u.Id,
                        FullName = u.FullName,
                        Email = u.Email,
                        PhoneNumber = u.PhoneNumber,
                        Address = u.Address,
                        Role = (u.Email.ToLower() == "admin@shms.com")
                            ? (u.Admin != null && !string.IsNullOrWhiteSpace(u.Admin.Role) ? u.Admin.Role : "SuperAdmin")
                            : (u.Admin != null && !string.IsNullOrWhiteSpace(u.Admin.Role) ? u.Admin.Role : "Admin"),
                        IsActive = u.IsActive,
                        RegDate = u.RegDate,
                        LastLogin = u.Admin != null ? u.Admin.LastLogin : null,
                        ProfilePic = u.ProfilePic ?? (u.Admin != null ? u.Admin.ProfilePic : null)
                    })
                    .ToListAsync();

                return Ok(adminUsers);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Failed to load admin accounts: " + ex.Message });
            }
        }

        [HttpPost("admins")]
        public async Task<IActionResult> CreateAdmin([FromBody] CreateAdminDto request)
        {
            try
            {
                var (isAllowed, errorMsg) = await EnsureSuperAdminAsync();
                if (!isAllowed)
                {
                    return StatusCode(403, new { message = errorMsg });
                }

                if (request == null || string.IsNullOrWhiteSpace(request.FullName) || string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Password))
                {
                    return BadRequest(new { message = "Full name, valid email, and secure password are required." });
                }

                // 1. Verify real DNS email domain
                var isValidDomain = await _emailVerificationService.IsRealEmailDomainAsync(request.Email);
                if (!isValidDomain)
                {
                    return BadRequest(new { message = "Please provide an active email with a valid domain (e.g. @gmail.com, @shms.com)." });
                }

                var normalizedEmail = request.Email.Trim().ToLower();
                var exists = await _context.Users.AnyAsync(u => u.Email.ToLower() == normalizedEmail);
                if (exists)
                {
                    return BadRequest(new { message = "An account with this email address already exists." });
                }

                if (request.Password.Length < 8)
                {
                    return BadRequest(new { message = "Password must be at least 8 characters." });
                }

                var user = new User
                {
                    FullName = request.FullName.Trim(),
                    Email = request.Email.Trim(),
                    PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password),
                    PhoneNumber = request.PhoneNumber?.Trim() ?? string.Empty,
                    Address = request.Address,
                    UserType = UserType.Admin,
                    RegDate = DateTime.UtcNow,
                    IsActive = true,
                    IsEmailVerified = true,
                    ProfilePic = request.ProfilePic,
                    Admin = new SmartHomeMaintenance.Domain.Entities.Admin
                    {
                        Role = string.IsNullOrWhiteSpace(request.Role) ? "Admin" : request.Role.Trim(),
                        ProfilePic = request.ProfilePic
                    }
                };

                _context.Users.Add(user);
                await _context.SaveChangesAsync();

                return Ok(new { success = true, message = "Admin account created successfully." });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Failed to create admin: " + ex.Message });
            }
        }

        [HttpPut("admins/{id}")]
        public async Task<IActionResult> UpdateAdmin(int id, [FromBody] UpdateAdminDto request)
        {
            try
            {
                var (isAllowed, errorMsg) = await EnsureSuperAdminAsync();
                if (!isAllowed)
                {
                    return StatusCode(403, new { message = errorMsg });
                }

                var user = await _context.Users
                    .Include(u => u.Admin)
                    .FirstOrDefaultAsync(u => u.Id == id && u.UserType == UserType.Admin);

                if (user == null)
                {
                    return NotFound(new { message = "Admin account not found." });
                }

                user.FullName = request.FullName.Trim();
                user.PhoneNumber = request.PhoneNumber?.Trim() ?? string.Empty;
                user.Address = request.Address;

                if (!string.IsNullOrEmpty(request.ProfilePic))
                {
                    user.ProfilePic = request.ProfilePic;
                }

                if (user.Admin != null)
                {
                    // If this is primary SuperAdmin (e.g. admin@shms.com), preserve SuperAdmin role
                    if (user.Email.ToLower() == "admin@shms.com")
                    {
                        user.Admin.Role = "SuperAdmin";
                    }
                    else if (!string.IsNullOrWhiteSpace(request.Role))
                    {
                        user.Admin.Role = request.Role.Trim();
                    }

                    if (!string.IsNullOrEmpty(request.ProfilePic))
                    {
                        user.Admin.ProfilePic = request.ProfilePic;
                    }
                }

                // If new password provided
                if (!string.IsNullOrWhiteSpace(request.NewPassword))
                {
                    if (request.NewPassword.Length < 8)
                    {
                        return BadRequest(new { message = "New password must be at least 8 characters." });
                    }
                    user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.NewPassword);
                }

                await _context.SaveChangesAsync();
                return Ok(new { success = true, message = "Admin account updated successfully." });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Failed to update admin: " + ex.Message });
            }
        }

        [HttpPut("admins/{id}/status")]
        public async Task<IActionResult> ToggleAdminStatus(int id, [FromQuery] bool isActive)
        {
            try
            {
                var (isAllowed, errorMsg) = await EnsureSuperAdminAsync();
                if (!isAllowed)
                {
                    return StatusCode(403, new { message = errorMsg });
                }

                var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == id && u.UserType == UserType.Admin);
                if (user == null)
                {
                    return NotFound(new { message = "Admin account not found." });
                }

                if (user.Email.ToLower() == "admin@shms.com" && !isActive)
                {
                    return BadRequest(new { message = "Primary Super Admin account cannot be deactivated or blocked." });
                }

                user.IsActive = isActive;
                await _context.SaveChangesAsync();

                return Ok(new { success = true, message = isActive ? "Admin account activated." : "Admin account blocked." });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Failed to update status: " + ex.Message });
            }
        }

        [HttpDelete("admins/{id}")]
        public async Task<IActionResult> DeleteAdmin(int id)
        {
            try
            {
                var (isAllowed, errorMsg) = await EnsureSuperAdminAsync();
                if (!isAllowed)
                {
                    return StatusCode(403, new { message = errorMsg });
                }

                var user = await _context.Users
                    .Include(u => u.Admin)
                    .FirstOrDefaultAsync(u => u.Id == id && u.UserType == UserType.Admin);

                if (user == null)
                {
                    return NotFound(new { message = "Admin account not found." });
                }

                if (user.Email.ToLower() == "admin@shms.com")
                {
                    return BadRequest(new { message = "Primary Super Admin account cannot be deleted." });
                }

                if (user.Admin != null)
                {
                    _context.Admins.Remove(user.Admin);
                }
                _context.Users.Remove(user);
                await _context.SaveChangesAsync();

                return Ok(new { success = true, message = "Admin account deleted successfully." });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Failed to delete admin: " + ex.Message });
            }
        }

        #endregion
    }

    public class UpdateAdminProfileRequest
    {
        public string? FullName { get; set; }
        public string? Email { get; set; }
        public string? PhoneNumber { get; set; }
        public string? Address { get; set; }
        public string? ProfilePic { get; set; }
    }

    public class AdminChangePasswordRequest
    {
        public string CurrentPassword { get; set; } = string.Empty;
        public string NewPassword { get; set; } = string.Empty;
    }

    public class ResetPasswordRequest
    {
        [System.ComponentModel.DataAnnotations.Required]
        [System.ComponentModel.DataAnnotations.MinLength(8)]
        public string NewPassword { get; set; } = string.Empty;
    }

    public class ApproveProviderBody
    {
        public string? Reason { get; set; }
        public bool RequestMoreInfo { get; set; } = false;
    }

    public class SendMessageToProviderRequest
    {
        public string? Subject { get; set; }
        public string Message { get; set; } = string.Empty;
    }
}
