using System;

namespace SmartHomeMaintenance.Domain.Entities
{
    public class Complaint
    {
        public int Id { get; set; }
        public string ComplaintCode { get; set; } = string.Empty;
        public string? BookingCode { get; set; }
        public int CustomerId { get; set; }
        public string CustomerName { get; set; } = string.Empty;
        public int ProviderId { get; set; }
        public string ProviderName { get; set; } = string.Empty;
        public string Subject { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Status { get; set; } = "Pending"; // Pending | In Review | Resolved | Dismissed
        public string Severity { get; set; } = "Normal"; // Normal | High | Critical
        public string? EvidenceUrl { get; set; }
        public string? AdminNotes { get; set; }
        public string? DismissReason { get; set; }
        public int WarningsIssued { get; set; } = 0;
        public string FiledAt { get; set; } = string.Empty;
        public DateTime? ResolvedAt { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
