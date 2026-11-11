using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SmartHomeMaintenance.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Text;

namespace SmartHomeMaintenance.Infrastructure.Data.Configurations
{
    public class AdminConfiguration : IEntityTypeConfiguration<Admin>
    {
        public void Configure(EntityTypeBuilder<Admin> builder)
        {
            builder.ToTable("Admin");

            builder.HasKey(a => a.AdminId);

            builder.Property(a => a.AdminId)
                .ValueGeneratedNever();

            builder.Property(a => a.Role)
                .IsRequired()
                .HasMaxLength(50)
                .HasDefaultValue("SuperAdmin");

            builder.Property(a => a.LastLogin);

            builder.Property(a => a.ProfilePic)
                .HasMaxLength(500);
        }
    }
}
