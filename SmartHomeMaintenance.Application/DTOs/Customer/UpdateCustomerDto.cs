using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text;

namespace SmartHomeMaintenance.Application.DTOs.Customer
{
    public class UpdateCustomerDto
    {
        [Required]
        [MaxLength(100)]
        public string FullName { get; set; } = string.Empty;

        [Required]
        public string PhoneNumber { get; set; } = string.Empty;

        public string? Address { get; set; }
        public string? PreferredPayment { get; set; }
        public string? DefaultAddress { get; set; }
        public string? ProfilePic { get; set; }
        public string? SavedAddressesJson { get; set; }
        public string? SavedSearchesJson { get; set; }
    }
}
