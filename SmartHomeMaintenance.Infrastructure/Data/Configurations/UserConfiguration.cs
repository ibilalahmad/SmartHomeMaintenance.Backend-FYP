using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SmartHomeMaintenance.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Text;

namespace SmartHomeMaintenance.Infrastructure.Data.Configurations
{
    public class UserConfiguration : IEntityTypeConfiguration<User>
    {
        public void Configure(EntityTypeBuilder<User> builder)
        {
            builder.ToTable("Users");

            builder.HasKey(u => u.Id);
            builder.Property(u => u.Id).ValueGeneratedOnAdd();

            builder.Property(u => u.FullName)
                .IsRequired()
                .HasMaxLength(100);

            builder.Property(u => u.Email)
                .IsRequired()
                .HasMaxLength(100);

            builder.HasIndex(u => u.Email)
                .IsUnique();

            builder.Property(u => u.PasswordHash)
                .IsRequired()
                .HasMaxLength(255);

            builder.Property(u => u.PhoneNumber)
                .IsRequired()
                .HasMaxLength(20);

            builder.Property(u => u.Address)
                .HasMaxLength(255);

            builder.Property(u => u.UserType)
                .IsRequired()
                .HasConversion<string>()
                .HasMaxLength(20);

            builder.Property(u => u.RegDate)
                .IsRequired()
                .HasDefaultValueSql("GETDATE()");

            builder.Property(u => u.IsActive)
                .IsRequired()
                .HasDefaultValue(true);

            builder.Property(u => u.IsEmailVerified)
                .IsRequired()
                .HasDefaultValue(false);

            builder.Property(u => u.ProfilePic)
                .HasMaxLength(500);

            // Configure 1:1 Navigation Relationships
            builder.HasOne(u => u.Customer)
                .WithOne(c => c.User)
                .HasForeignKey<Customer>(c => c.CustomerId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasOne(u => u.ServiceProvider)
                .WithOne(sp => sp.User)
                .HasForeignKey<ServiceProvider>(sp => sp.ProviderId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasOne(u => u.Admin)
                .WithOne(a => a.User)
                .HasForeignKey<Admin>(a => a.AdminId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
