using System;
using System.Collections.Generic;
using System.Text;

namespace SmartHomeMaintenance.Application.DTOs.Customer
{
    public class CustomerProfileDto
    {
        public int CustomerId { get; set; }
        public string FullName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string PhoneNumber { get; set; } = string.Empty;
        public string? Address { get; set; }
        public string? PreferredPayment { get; set; }
        public string? DefaultAddress { get; set; }
        public int LoyaltyPoints { get; set; }
        public string? ProfilePic { get; set; }
        public string? SavedAddressesJson { get; set; }
        public string? SavedSearchesJson { get; set; }
        public bool IsTemporarilyDeactivated { get; set; }
        public DateTime RegDate { get; set; }
        public bool IsActive { get; set; }
    }
}
