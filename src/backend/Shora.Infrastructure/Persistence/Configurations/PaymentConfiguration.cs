using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Shora.Domain.Entities;

namespace Shora.Infrastructure.Persistence.Configurations;

public class PaymentConfiguration : IEntityTypeConfiguration<Payment>
{
    public void Configure(EntityTypeBuilder<Payment> builder)
    {
        builder.HasIndex(p => p.BookingId)
            .IsUnique();

        builder.Property(p => p.Amount)
            .HasPrecision(10, 2);

        builder.Property(p => p.Currency)
            .HasMaxLength(3)
            .IsRequired();

        builder.Property(p => p.Method)
            .HasConversion<string>()
            .HasMaxLength(32);

        builder.Property(p => p.Status)
            .HasConversion<string>()
            .HasMaxLength(32);

        builder.Property(p => p.RefundReference)
            .HasMaxLength(500);

        builder.Property(p => p.RefundRevocationReason)
            .HasMaxLength(1000);

        builder.HasOne(p => p.RefundedByAdmin)
            .WithMany()
            .HasForeignKey(p => p.RefundedByAdminId)
            .OnDelete(DeleteBehavior.NoAction);

        builder.HasOne(p => p.RefundRevokedByAdmin)
            .WithMany()
            .HasForeignKey(p => p.RefundRevokedByAdminId)
            .OnDelete(DeleteBehavior.NoAction);
    }
}
