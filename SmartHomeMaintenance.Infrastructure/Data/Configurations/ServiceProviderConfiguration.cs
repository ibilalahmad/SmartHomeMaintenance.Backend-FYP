using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SmartHomeMaintenance.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Text;

namespace SmartHomeMaintenance.Infrastructure.Data.Configurations
{
    public class ServiceProviderConfiguration : IEntityTypeConfiguration<ServiceProvider>
    {
        public void Configure(EntityTypeBuilder<ServiceProvider> builder)
        {
            builder.ToTable("Service_Providers");

            builder.HasKey(sp => sp.ProviderId);

            builder.Property(sp => sp.ProviderId)
                .ValueGeneratedNever();

            builder.Property(sp => sp.WorkExperience)
                .IsRequired()
                .HasDefaultValue(0);

            builder.Property(sp => sp.Expertise)
                .IsRequired()
                .HasMaxLength(255);

            builder.Property(sp => sp.Qualification)
                .HasMaxLength(200);

            builder.Property(sp => sp.HourlyRate)
                .HasColumnType("decimal(10,2)")
                .HasDefaultValue(0.00m);

            builder.Property(sp => sp.IsApproved)
                .IsRequired()
                .HasDefaultValue(false);

            builder.Property(sp => sp.AvgRating)
                .HasColumnType("decimal(3,2)")
                .HasDefaultValue(0.00m);

            builder.Property(sp => sp.TotalBookings)
                .IsRequired()
                .HasDefaultValue(0);

            builder.Property(sp => sp.Location)
                .IsRequired()
                .HasMaxLength(200);

            builder.Property(sp => sp.Latitude)
                .HasColumnType("decimal(10,8)");

            builder.Property(sp => sp.Longitude)
                .HasColumnType("decimal(11,8)");

            builder.Property(sp => sp.ProfilePic)
                .HasMaxLength(500);
        }
    }
}
