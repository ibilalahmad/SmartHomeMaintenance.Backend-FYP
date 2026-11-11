using System;
using System.Collections.Generic;
using System.Text;

namespace SmartHomeMaintenance.Domain.Entities
{
    public class ServiceProvider
    {
        // Primary Key and Foreign Key linking to User.Id
        public int ProviderId { get; set; }
        public int WorkExperience { get; set; }
        public string Expertise { get; set; } = string.Empty;
        public string? Qualification { get; set; }
        public decimal HourlyRate { get; set; } = 0.00m;
        public bool IsApproved { get; set; } = false;
        public decimal AvgRating { get; set; } = 0.00m;
        public int TotalBookings { get; set; } = 0;
        public string Location { get; set; } = string.Empty;
        public decimal? Latitude { get; set; }
        public decimal? Longitude { get; set; }
        public string? ProfilePic { get; set; }
        public bool IsAvailable { get; set; } = true;
        public string? RejectionReason { get; set; }

        // Navigation Property
        public virtual User User { get; set; } = null!;
    }
}
