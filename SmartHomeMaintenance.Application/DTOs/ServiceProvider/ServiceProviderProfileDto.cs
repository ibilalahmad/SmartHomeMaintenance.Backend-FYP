using System;
using System.Collections.Generic;
using System.Text;

namespace SmartHomeMaintenance.Application.DTOs.ServiceProvider
{
    public class ServiceProviderProfileDto
    {
        public int ProviderId { get; set; }
        public string FullName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string PhoneNumber { get; set; } = string.Empty;
        public string? Address { get; set; }
        public int WorkExperience { get; set; }
        public string Expertise { get; set; } = string.Empty;
        public string? Qualification { get; set; }
        public decimal HourlyRate { get; set; }
        public bool IsApproved { get; set; }
        public decimal AvgRating { get; set; }
        public int TotalBookings { get; set; }
        public string Location { get; set; } = string.Empty;
        public decimal? Latitude { get; set; }
        public decimal? Longitude { get; set; }
        public string? ProfilePic { get; set; }
        public bool IsActive { get; set; }
        public bool IsAvailable { get; set; } = true;
        public string? RejectionReason { get; set; }
        public bool IsTemporarilyDeactivated { get; set; }
        public DateTime? RegistrationDate { get; set; }
        public DateTime? RegDate { get; set; }
    }
}
