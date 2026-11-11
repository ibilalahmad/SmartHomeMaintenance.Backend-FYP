using System;
using System.Collections.Generic;
using System.Text;

namespace SmartHomeMaintenance.Domain.Entities
{
    public class Customer
    {
        // Primary Key and Foreign Key linking to User.Id
        public int CustomerId { get; set; }
        public string? PreferredPayment { get; set; }
        public string? DefaultAddress { get; set; }
        public int LoyaltyPoints { get; set; } = 0;
        public string? ProfilePic { get; set; }
        public string? SavedAddressesJson { get; set; }
        public string? SavedSearchesJson { get; set; }

        // Navigation Property
        public virtual User User { get; set; } = null!;
    }
}
