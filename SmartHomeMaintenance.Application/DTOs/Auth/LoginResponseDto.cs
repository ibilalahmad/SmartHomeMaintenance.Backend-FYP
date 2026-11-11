using System;
using System.Collections.Generic;
using System.Text;

namespace SmartHomeMaintenance.Application.DTOs.Auth
{
    public class LoginResponseDto
    {
        public int UserId { get; set; }
        public string FullName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string UserType { get; set; } = string.Empty;
        public string? AdminRole { get; set; }
        public string? ProfilePic { get; set; }
        public bool IsActive { get; set; }
        public bool? IsApproved { get; set; } // Nullable for non-service providers
        public string Token { get; set; } = string.Empty; // Session/JWT token placeholder
        public string Message { get; set; } = string.Empty;
    }
}
