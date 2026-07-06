using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Shora.Domain.Entities;

namespace Shora.Infrastructure.Persistence.Configurations;

public class PaymentReceiptConfiguration : IEntityTypeConfiguration<PaymentReceipt>
{
    public void Configure(EntityTypeBuilder<PaymentReceipt> builder)
    {
        builder.HasIndex(r => r.ContentHashSha256);

        builder.Property(r => r.BlobPath)
            .HasMaxLength(500)
            .IsRequired();

        builder.Property(r => r.OriginalFileName)
            .HasMaxLength(260)
            .IsRequired();

        builder.Property(r => r.ContentType)
            .HasMaxLength(128)
            .IsRequired();

        builder.Property(r => r.ContentHashSha256)
            .HasMaxLength(64)
            .IsRequired();

        builder.Property(r => r.SenderReference)
            .HasMaxLength(200);

        builder.Property(r => r.BlobState)
            .HasConversion<string>()
            .HasMaxLength(32);

        builder.Property(r => r.MalwareScanStatus)
            .HasConversion<string>()
            .HasMaxLength(32);

        builder.Property(r => r.ReviewStatus)
            .HasConversion<string>()
            .HasMaxLength(32);

        builder.Property(r => r.DeclineReasonCode)
            .HasConversion<string>()
            .HasMaxLength(32);

        builder.Property(r => r.DeclineReason)
            .HasMaxLength(1000);

        builder.HasOne(r => r.Payment)
            .WithMany(p => p.Receipts)
            .HasForeignKey(r => r.PaymentId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(r => r.ReviewedByAdmin)
            .WithMany()
            .HasForeignKey(r => r.ReviewedByAdminId)
            .OnDelete(DeleteBehavior.NoAction);
    }
}
