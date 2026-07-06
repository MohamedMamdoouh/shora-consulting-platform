using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Shora.Domain.Entities;

namespace Shora.Infrastructure.Persistence.Configurations;

public class BookingStatusAuditConfiguration : IEntityTypeConfiguration<BookingStatusAudit>
{
    public void Configure(EntityTypeBuilder<BookingStatusAudit> builder)
    {
        builder.Property(a => a.FromStatus)
            .HasConversion<string>()
            .HasMaxLength(32);

        builder.Property(a => a.ToStatus)
            .HasConversion<string>()
            .HasMaxLength(32);

        builder.Property(a => a.Actor)
            .HasConversion<string>()
            .HasMaxLength(16);

        builder.Property(a => a.Reason)
            .HasMaxLength(1000);

        builder.HasOne(a => a.Booking)
            .WithMany(b => b.StatusAudits)
            .HasForeignKey(a => a.BookingId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(a => a.ActorUser)
            .WithMany()
            .HasForeignKey(a => a.ActorUserId)
            .OnDelete(DeleteBehavior.NoAction);
    }
}
