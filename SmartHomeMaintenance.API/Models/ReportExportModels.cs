using System;
using System.Collections.Generic;

namespace SmartHomeMaintenance.API.Models
{
    public class BookingReportSummaryDto
    {
        public int Total { get; set; }
        public int Completed { get; set; }
        public int Cancelled { get; set; }
        public int Pending { get; set; }
        public double SuccessRate { get; set; }
    }

    public class SPPerformanceReportSummaryDto
    {
        public int TotalProviders { get; set; }
        public int ActiveProviders { get; set; }
        public int TotalJobsCompleted { get; set; }
        public double AverageRating { get; set; }
    }

    public class SPPerformanceReportItemDto
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string? Expertise { get; set; }
        public decimal HourlyRate { get; set; }
        public int CompletedJobs { get; set; }
        public double AverageRating { get; set; }
        public int ComplaintsReceived { get; set; }
        public string Status { get; set; } = "Active";
    }

    public class CustomerActivityReportSummaryDto
    {
        public int TotalRegistered { get; set; }
        public int ActiveLast3Months { get; set; }
        public int TotalBookings { get; set; }
        public double AverageBookingsPerCustomer { get; set; }
    }

    public class CustomerActivityReportItemDto
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Phone { get; set; } = string.Empty;
        public string Location { get; set; } = string.Empty;
        public int BookingsCount { get; set; }
        public int ComplaintsCount { get; set; }
        public string Status { get; set; } = "Active";
    }

    public class ExportReportRequestDto
    {
        public string? Title { get; set; }
        public string ReportType { get; set; } = "SPPerformance"; // "SPPerformance" or "CustomerActivity"
        public string? StartDate { get; set; }
        public string? EndDate { get; set; }
        public string? GeneratedBy { get; set; }

        public BookingReportSummaryDto? BookingSummary { get; set; }
        public SPPerformanceReportSummaryDto? SpSummary { get; set; }
        public List<SPPerformanceReportItemDto>? SpPerformance { get; set; }
        public CustomerActivityReportSummaryDto? CustomerSummary { get; set; }
        public List<CustomerActivityReportItemDto>? CustomerItems { get; set; }
    }
}
