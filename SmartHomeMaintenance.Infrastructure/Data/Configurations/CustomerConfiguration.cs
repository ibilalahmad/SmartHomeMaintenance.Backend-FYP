using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SmartHomeMaintenance.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Text;

namespace SmartHomeMaintenance.Infrastructure.Data.Configurations
{
    public class CustomerConfiguration : IEntityTypeConfiguration<Customer>
    {
        public void Configure(EntityTypeBuilder<Customer> builder)
        {
            builder.ToTable("Customers");

            builder.HasKey(c => c.CustomerId);

            builder.Property(c => c.CustomerId)
                .ValueGeneratedNever();

            builder.Property(c => c.PreferredPayment)
                .HasMaxLength(50);

            builder.Property(c => c.DefaultAddress)
                .HasMaxLength(255);

            builder.Property(c => c.LoyaltyPoints)
                .IsRequired()
                .HasDefaultValue(0);

            builder.Property(c => c.ProfilePic)
                .HasMaxLength(500);
        }
    }
}
