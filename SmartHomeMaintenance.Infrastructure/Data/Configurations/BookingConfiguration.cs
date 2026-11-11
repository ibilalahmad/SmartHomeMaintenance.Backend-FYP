using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SmartHomeMaintenance.Domain.Entities;

namespace SmartHomeMaintenance.Infrastructure.Data.Configurations
{
    public class BookingConfiguration : IEntityTypeConfiguration<Booking>
    {
        public void Configure(EntityTypeBuilder<Booking> builder)
        {
            builder.ToTable("Bookings");

            builder.HasKey(b => b.Id);

            builder.Property(b => b.BookingCode)
                .IsRequired()
                .HasMaxLength(30);

            builder.HasIndex(b => b.BookingCode)
                .IsUnique();

            builder.Property(b => b.CustomerName)
                .IsRequired()
                .HasMaxLength(150);

            builder.Property(b => b.CustomerPhone)
                .HasMaxLength(30);

            builder.Property(b => b.ProviderName)
                .IsRequired()
                .HasMaxLength(150);

            builder.Property(b => b.ServiceType)
                .IsRequired()
                .HasMaxLength(150);

            builder.Property(b => b.ScheduledWhen)
                .IsRequired()
                .HasMaxLength(50);

            builder.Property(b => b.Price)
                .HasColumnType("decimal(10,2)")
                .HasDefaultValue(0.00m);

            builder.Property(b => b.Status)
                .IsRequired()
                .HasMaxLength(30)
                .HasDefaultValue("Pending");

            builder.Property(b => b.Address)
                .IsRequired()
                .HasMaxLength(500);

            builder.Property(b => b.Instructions)
                .HasMaxLength(1000);

            builder.Property(b => b.PaymentStatus)
                .IsRequired()
                .HasMaxLength(30)
                .HasDefaultValue("Pending");

            builder.Property(b => b.PaymentMethod)
                .HasMaxLength(50);

            builder.Property(b => b.PaymentChannel)
                .HasMaxLength(50);

            builder.Property(b => b.PaymentReference)
                .HasMaxLength(100);

            builder.Property(b => b.PaymentProofUrl)
                .HasMaxLength(500);

            builder.Property(b => b.PaymentNote)
                .HasMaxLength(500);

            builder.Property(b => b.CancelReason)
                .HasMaxLength(500);

            builder.Property(b => b.ProposedWhen)
                .HasMaxLength(50);

            builder.Property(b => b.ProposalNote)
                .HasMaxLength(500);

            builder.Property(b => b.ProposalStatus)
                .HasMaxLength(40);

            builder.Property(b => b.CreatedAt)
                .IsRequired();

            builder.HasOne(b => b.Customer)
                .WithMany()
                .HasForeignKey(b => b.CustomerId)
                .OnDelete(DeleteBehavior.NoAction);

            builder.HasOne(b => b.ServiceProvider)
                .WithMany()
                .HasForeignKey(b => b.ProviderId)
                .OnDelete(DeleteBehavior.NoAction);
        }
    }
}
