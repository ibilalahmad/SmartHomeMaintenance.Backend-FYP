using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text;

namespace SmartHomeMaintenance.Application.DTOs.Auth
{
    public class ServiceProviderRegisterDto
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

        [Required]
        public string Expertise { get; set; } = string.Empty;

        public int WorkExperience { get; set; }
        public string? Qualification { get; set; }
        public decimal HourlyRate { get; set; }

        [Required]
        public string Location { get; set; } = string.Empty;

        public decimal? Latitude { get; set; }
        public decimal? Longitude { get; set; }

        public string? Address { get; set; }
        public string? ProfilePic { get; set; }
    }
}
