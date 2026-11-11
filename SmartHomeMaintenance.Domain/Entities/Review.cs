using System;

namespace SmartHomeMaintenance.Domain.Entities
{
    public class Review
    {
        public int Id { get; set; }
        public string ReviewCode { get; set; } = string.Empty;
        public string? BookingCode { get; set; }

        public int CustomerId { get; set; }
        public string CustomerName { get; set; } = string.Empty;

        public int ProviderId { get; set; }
        public string ProviderName { get; set; } = string.Empty;

        public int Stars { get; set; }
        public string Comment { get; set; } = string.Empty;
        public string Status { get; set; } = "Published";
        public string? RemovedAt { get; set; }
        public string? Reply { get; set; }
        public DateTime? ReplyAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // Navigation properties
        public virtual Customer Customer { get; set; } = null!;
        public virtual ServiceProvider ServiceProvider { get; set; } = null!;
    }
}
