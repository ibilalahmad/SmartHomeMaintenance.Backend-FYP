using System;

namespace SmartHomeMaintenance.Domain.Entities
{
    public class CustomerFavorite
    {
        public int Id { get; set; }
        public int CustomerId { get; set; }
        public int ProviderId { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
