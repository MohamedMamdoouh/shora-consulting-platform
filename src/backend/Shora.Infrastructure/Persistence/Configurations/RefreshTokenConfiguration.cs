using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Shora.Domain.Entities;

namespace Shora.Infrastructure.Persistence.Configurations;

public class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
{
    public void Configure(EntityTypeBuilder<RefreshToken> builder)
    {
        builder.HasIndex(t => t.TokenHash)
            .IsUnique();

        builder.Property(t => t.TokenHash)
            .HasMaxLength(64)
            .IsRequired();

        builder.Property(t => t.ReplacedByTokenHash)
            .HasMaxLength(64);

        builder.Property(t => t.CreatedByIp)
            .HasMaxLength(64);

        builder.Property(t => t.UserAgent)
            .HasMaxLength(512);

        builder.HasOne(t => t.User)
            .WithMany(u => u.RefreshTokens)
            .HasForeignKey(t => t.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
