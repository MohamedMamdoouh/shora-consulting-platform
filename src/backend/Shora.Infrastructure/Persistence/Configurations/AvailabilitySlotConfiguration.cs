using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Shora.Domain.Entities;

namespace Shora.Infrastructure.Persistence.Configurations;

public class AvailabilitySlotConfiguration : IEntityTypeConfiguration<AvailabilitySlot>
{
    public void Configure(EntityTypeBuilder<AvailabilitySlot> builder)
    {
        builder.HasIndex(s => s.StartTime)
            .IsUnique();

        builder.HasOne(s => s.Booking)
            .WithOne()
            .HasForeignKey<AvailabilitySlot>(s => s.BookingId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasIndex(s => s.BookingId)
            .IsUnique()
            .HasFilter("[BookingId] IS NOT NULL");
    }
}
