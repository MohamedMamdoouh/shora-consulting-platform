using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Shora.Domain.Entities;

namespace Shora.Infrastructure.Persistence.Configurations;

public class OutboxMessageConfiguration : IEntityTypeConfiguration<OutboxMessage>
{
    public void Configure(EntityTypeBuilder<OutboxMessage> builder)
    {
        builder.HasIndex(m => m.IdempotencyKey)
            .IsUnique();

        builder.Property(m => m.MessageType)
            .HasMaxLength(128)
            .IsRequired();

        builder.Property(m => m.AggregateType)
            .HasMaxLength(64)
            .IsRequired();

        builder.Property(m => m.IdempotencyKey)
            .HasMaxLength(256)
            .IsRequired();

        builder.Property(m => m.Status)
            .HasConversion<string>()
            .HasMaxLength(32);

        builder.Property(m => m.LastError)
            .HasMaxLength(2000);
    }
}
