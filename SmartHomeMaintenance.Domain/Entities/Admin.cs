using System;
using System.Collections.Generic;
using System.Text;

namespace SmartHomeMaintenance.Domain.Entities
{
    public class Admin
    {
        // Primary Key and Foreign Key linking to User.Id
        public int AdminId { get; set; }
        public string Role { get; set; } = "SuperAdmin";
        public DateTime? LastLogin { get; set; }
        public string? ProfilePic { get; set; }

        // Navigation Property
        public virtual User User { get; set; } = null!;
    }
}
