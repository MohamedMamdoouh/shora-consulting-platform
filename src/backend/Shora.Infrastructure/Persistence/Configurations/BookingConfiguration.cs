using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Shora.Domain.Entities;

namespace Shora.Infrastructure.Persistence.Configurations;

public class BookingConfiguration : IEntityTypeConfiguration<Booking>
{
    public void Configure(EntityTypeBuilder<Booking> builder)
    {
        builder.Property(b => b.ContactPhone)
            .HasMaxLength(32);

        builder.Property(b => b.Status)
            .HasConversion<string>()
            .HasMaxLength(32);

        builder.Property(b => b.DeliveryMethod)
            .HasConversion<string>()
            .HasMaxLength(16);

        builder.Property(b => b.RowVersion)
            .IsRowVersion();

        builder.HasOne(b => b.Client)
            .WithMany(u => u.Bookings)
            .HasForeignKey(b => b.ClientId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(b => b.AvailabilitySlot)
            .WithMany()
            .HasForeignKey(b => b.AvailabilitySlotId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(b => b.Payment)
            .WithOne(p => p.Booking)
            .HasForeignKey<Payment>(p => p.BookingId);

        builder.HasOne(b => b.CancellationRequest)
            .WithOne(c => c.Booking)
            .HasForeignKey<CancellationRequest>(c => c.BookingId);

        builder.HasIndex(b => b.AvailabilitySlotId)
            .IsUnique()
            .HasFilter(
                "[AvailabilitySlotId] IS NOT NULL AND [Status] IN (" +
                "'PendingPayment', 'PendingApproval', 'Confirmed', 'CancellationRequested', 'Completed')");
    }
}
