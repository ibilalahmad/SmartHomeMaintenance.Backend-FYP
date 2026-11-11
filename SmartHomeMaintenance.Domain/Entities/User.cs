using SmartHomeMaintenance.Domain.Common;
using SmartHomeMaintenance.Domain.Enums;
using System;
using System.Collections.Generic;
using System.Text;

namespace SmartHomeMaintenance.Domain.Entities
{
    public class User : BaseEntity
    {
        public string FullName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string PasswordHash { get; set; } = string.Empty;
        public string PhoneNumber { get; set; } = string.Empty;
        public string? Address { get; set; }
        public UserType UserType { get; set; }
        public DateTime RegDate { get; set; } = DateTime.UtcNow;
        public bool IsActive { get; set; } = true;
        public bool IsEmailVerified { get; set; } = false;
        public string? ProfilePic { get; set; }
        public int FailedLoginAttempts { get; set; } = 0;
        public DateTime? LockoutEnd { get; set; }
        public bool IsTemporarilyDeactivated { get; set; } = false;

        // Navigation Properties (1:1 Relationships)
        public virtual Customer? Customer { get; set; }
        public virtual ServiceProvider? ServiceProvider { get; set; }
        public virtual Admin? Admin { get; set; }
    }
}
