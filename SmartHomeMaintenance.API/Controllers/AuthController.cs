using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using SmartHomeMaintenance.Application.Common.Interfaces;
using SmartHomeMaintenance.Application.DTOs.Auth;
using System;
using System.Security.Claims;
using System.Threading.Tasks;

namespace SmartHomeMaintenance.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly IAuthService _authService;

        public AuthController(IAuthService authService)
        {
            _authService = authService;
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginRequestDto request)
        {
            try
            {
                var result = await _authService.LoginAsync(request);
                return Ok(result);
            }
            catch (UnauthorizedAccessException ex)
            {
                return Unauthorized(new { message = ex.Message });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "An error occurred while processing your request.", details = ex.Message });
            }
        }

        [HttpPost("register/customer")]
        public async Task<IActionResult> RegisterCustomer([FromBody] CustomerRegisterDto request)
        {
            try
            {
                var result = await _authService.RegisterCustomerAsync(request);
                return Ok(result);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Registration failed.", details = ex.Message });
            }
        }

        [HttpPost("register/service-provider")]
        public async Task<IActionResult> RegisterServiceProvider([FromBody] ServiceProviderRegisterDto request)
        {
            try
            {
                var result = await _authService.RegisterServiceProviderAsync(request);
                return Ok(result);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Registration failed.", details = ex.Message });
            }
        }

        [HttpPost("verify-registration-otp")]
        public async Task<IActionResult> VerifyRegistrationOtp([FromBody] VerifyRegistrationOtpRequestDto request)
        {
            try
            {
                var result = await _authService.VerifyRegistrationOtpAsync(request);
                return Ok(result);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Verification failed.", details = ex.Message });
            }
        }

        [HttpPost("resend-registration-otp")]
        public async Task<IActionResult> ResendRegistrationOtp([FromBody] ResendRegistrationOtpRequestDto request)
        {
            try
            {
                var result = await _authService.ResendRegistrationOtpAsync(request.Email);
                return Ok(new { success = result, message = "A new 6-digit confirmation code has been sent to your email." });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Failed to resend code.", details = ex.Message });
            }
        }

        [HttpPost("forgot-password")]
        public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordRequestDto request)
        {
            if (string.IsNullOrWhiteSpace(request.Email))
            {
                return BadRequest(new { message = "Email is required." });
            }

            await _authService.ForgotPasswordAsync(request.Email);
            return Ok(new 
            { 
                success = true, 
                message = "If this email is registered in our system, a 6-digit verification code has been sent from ibilalahmad.dev@gmail.com." 
            });
        }

        [HttpPost("verify-code")]
        public async Task<IActionResult> VerifyResetCode([FromBody] VerifyResetCodeRequestDto request)
        {
            var isValid = await _authService.VerifyResetCodeAsync(request.Email, request.Code);
            if (!isValid)
            {
                return BadRequest(new { success = false, message = "Invalid or expired verification code." });
            }

            return Ok(new { success = true, message = "Verification code is valid." });
        }

        [HttpPost("reset-password")]
        public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordRequestDto request)
        {
            try
            {
                var result = await _authService.ResetPasswordWithCodeAsync(request.Email, request.Code, request.NewPassword);
                return Ok(new { success = result, message = "Your password has been successfully reset. You may now log in." });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Unable to reset password at this time.", details = ex.Message });
            }
        }

        [HttpGet("test-email")]
        public async Task<IActionResult> TestEmail([FromQuery] string to = "ibilalahmad.dev@gmail.com")
        {
            try
            {
                await _authService.ForgotPasswordAsync(to);
                return Ok(new { success = true, message = $"Test verification code email sent to {to} from ibilalahmad.dev@gmail.com" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, error = ex.Message });
            }
        }

        [Authorize]
        [HttpPost("change-password/{userId}")]
        public async Task<IActionResult> ChangePassword(int userId, [FromBody] ChangePasswordRequestDto request)
        {
            var currentUserIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!User.IsInRole("Admin") && (!int.TryParse(currentUserIdStr, out var currentUserId) || currentUserId != userId))
            {
                return Forbid();
            }

            try
            {
                var result = await _authService.ChangePasswordAsync(userId, request.OldPassword, request.NewPassword);
                return Ok(new { success = result, message = "Password changed successfully." });
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new { message = ex.Message });
            }
            catch (UnauthorizedAccessException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }
    }

    public class ChangePasswordRequestDto
    {
        public string OldPassword { get; set; } = string.Empty;
        public string NewPassword { get; set; } = string.Empty;
    }
}
