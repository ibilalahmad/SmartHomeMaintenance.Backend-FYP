using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text;

namespace SmartHomeMaintenance.Application.DTOs.ServiceProvider
{
    public class UpdateServiceProviderDto
    {
        [Required]
        [MaxLength(100)]
        public string FullName { get; set; } = string.Empty;

        [Required]
        public string PhoneNumber { get; set; } = string.Empty;

        public string? Address { get; set; }
        public int WorkExperience { get; set; }

        [Required]
        public string Expertise { get; set; } = string.Empty;

        public string? Qualification { get; set; }
        public decimal HourlyRate { get; set; }

        [Required]
        public string Location { get; set; } = string.Empty;

        public decimal? Latitude { get; set; }
        public decimal? Longitude { get; set; }

        public string? ProfilePic { get; set; }
        public bool? IsAvailable { get; set; }
    }
}
