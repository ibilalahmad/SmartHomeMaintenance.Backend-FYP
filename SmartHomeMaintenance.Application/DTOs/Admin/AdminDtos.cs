using System;
using System.ComponentModel.DataAnnotations;

namespace SmartHomeMaintenance.Application.DTOs.Admin
{
    public class AdminDto
    {
        public int AdminId { get; set; }
        public string FullName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string PhoneNumber { get; set; } = string.Empty;
        public string? Address { get; set; }
        public string Role { get; set; } = "Admin";
        public bool IsActive { get; set; } = true;
        public DateTime RegDate { get; set; }
        public DateTime? LastLogin { get; set; }
        public string? ProfilePic { get; set; }
    }

    public class CreateAdminDto
    {
        [Required]
        [MaxLength(100)]
        public string FullName { get; set; } = string.Empty;

        [Required]
        [EmailAddress]
        public string Email { get; set; } = string.Empty;

        [Required]
        [MinLength(8)]
        public string Password { get; set; } = string.Empty;

        [Required]
        public string PhoneNumber { get; set; } = string.Empty;

        public string? Address { get; set; }
        public string Role { get; set; } = "Admin";
        public string? ProfilePic { get; set; }
    }

    public class UpdateAdminDto
    {
        [Required]
        [MaxLength(100)]
        public string FullName { get; set; } = string.Empty;

        [Required]
        public string PhoneNumber { get; set; } = string.Empty;

        public string? Address { get; set; }
        public string Role { get; set; } = "Admin";
        public string? ProfilePic { get; set; }
        public string? NewPassword { get; set; }
    }
}
