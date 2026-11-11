using System;

namespace SmartHomeMaintenance.Domain.Entities
{
    public class BookingIssue
    {
        public int Id { get; set; }
        public string IssueCode { get; set; } = string.Empty;
        public string BookingCode { get; set; } = string.Empty;
        public int ProviderId { get; set; }
        public string ProviderName { get; set; } = string.Empty;
        public int CustomerId { get; set; }
        public string CustomerName { get; set; } = string.Empty;
        public string IssueType { get; set; } = "General Issue";
        public string Description { get; set; } = string.Empty;
        public string Status { get; set; } = "Open"; // Open | In Review | Resolved
        public string? AdminNotes { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
