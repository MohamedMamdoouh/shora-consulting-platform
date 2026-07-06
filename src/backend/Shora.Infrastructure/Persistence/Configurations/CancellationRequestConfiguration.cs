using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Shora.Domain.Entities;

namespace Shora.Infrastructure.Persistence.Configurations;

public class CancellationRequestConfiguration : IEntityTypeConfiguration<CancellationRequest>
{
    public void Configure(EntityTypeBuilder<CancellationRequest> builder)
    {
        builder.HasIndex(c => c.BookingId)
            .IsUnique();

        builder.Property(c => c.Status)
            .HasConversion<string>()
            .HasMaxLength(32);

        builder.Property(c => c.DecisionReasonCode)
            .HasConversion<string>()
            .HasMaxLength(32);

        builder.Property(c => c.ClientReason)
            .HasMaxLength(1000);

        builder.Property(c => c.DecisionReason)
            .HasMaxLength(1000);

        builder.HasOne(c => c.RequestedByClient)
            .WithMany()
            .HasForeignKey(c => c.RequestedByClientId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(c => c.ReviewedByAdmin)
            .WithMany()
            .HasForeignKey(c => c.ReviewedByAdminId)
            .OnDelete(DeleteBehavior.NoAction);
    }
}
