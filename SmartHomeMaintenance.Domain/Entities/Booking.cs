using System;

namespace SmartHomeMaintenance.Domain.Entities
{
    public class Booking
    {
        public int Id { get; set; }
        public string BookingCode { get; set; } = string.Empty;

        public int CustomerId { get; set; }
        public string CustomerName { get; set; } = string.Empty;
        public string? CustomerPhone { get; set; }

        public int ProviderId { get; set; }
        public string ProviderName { get; set; } = string.Empty;

        public string ServiceType { get; set; } = string.Empty;
        public string ScheduledWhen { get; set; } = string.Empty;
        public decimal Price { get; set; } = 0.00m;
        public string Status { get; set; } = "Pending";
        public string Address { get; set; } = string.Empty;
        public string? Instructions { get; set; }
        public string PaymentStatus { get; set; } = "Pending";
        public string? PaymentMethod { get; set; }
        public string? PaymentChannel { get; set; }
        public string? PaymentReference { get; set; }
        public string? PaymentProofUrl { get; set; }
        public string? PaymentNote { get; set; }
        public DateTime? PaymentSubmittedAt { get; set; }
        public DateTime? PaymentVerifiedAt { get; set; }
        public string? CancelReason { get; set; }
        public string? ProposedWhen { get; set; }
        public string? ProposalNote { get; set; }
        public string? ProposalStatus { get; set; }
        public bool ReminderSent { get; set; } = false;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // Navigation properties
        public virtual Customer Customer { get; set; } = null!;
        public virtual ServiceProvider ServiceProvider { get; set; } = null!;
    }
}
