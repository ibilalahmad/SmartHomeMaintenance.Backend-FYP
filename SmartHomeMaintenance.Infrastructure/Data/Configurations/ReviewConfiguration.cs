using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SmartHomeMaintenance.Domain.Entities;

namespace SmartHomeMaintenance.Infrastructure.Data.Configurations
{
    public class ReviewConfiguration : IEntityTypeConfiguration<Review>
    {
        public void Configure(EntityTypeBuilder<Review> builder)
        {
            builder.ToTable("Reviews");

            builder.HasKey(r => r.Id);

            builder.Property(r => r.ReviewCode)
                .IsRequired()
                .HasMaxLength(30);

            builder.HasIndex(r => r.ReviewCode)
                .IsUnique();

            builder.Property(r => r.BookingCode)
                .HasMaxLength(30);

            builder.Property(r => r.CustomerName)
                .IsRequired()
                .HasMaxLength(150);

            builder.Property(r => r.ProviderName)
                .IsRequired()
                .HasMaxLength(150);

            builder.Property(r => r.Stars)
                .IsRequired();

            builder.Property(r => r.Comment)
                .IsRequired()
                .HasMaxLength(1000);

            builder.Property(r => r.Status)
                .IsRequired()
                .HasMaxLength(30)
                .HasDefaultValue("Published");

            builder.Property(r => r.RemovedAt)
                .HasMaxLength(60);

            builder.Property(r => r.CreatedAt)
                .IsRequired();

            builder.HasOne(r => r.Customer)
                .WithMany()
                .HasForeignKey(r => r.CustomerId)
                .OnDelete(DeleteBehavior.NoAction);

            builder.HasOne(r => r.ServiceProvider)
                .WithMany()
                .HasForeignKey(r => r.ProviderId)
                .OnDelete(DeleteBehavior.NoAction);
        }
    }
}
