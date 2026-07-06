using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Shora.Domain.Entities;

namespace Shora.Infrastructure.Persistence.Configurations;

public class BlockedDateConfiguration : IEntityTypeConfiguration<BlockedDate>
{
    public void Configure(EntityTypeBuilder<BlockedDate> builder)
    {
        builder.Property(b => b.Reason)
            .HasMaxLength(500);
    }
}
