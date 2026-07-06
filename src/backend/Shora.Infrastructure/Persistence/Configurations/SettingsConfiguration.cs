using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Shora.Domain.Entities;

namespace Shora.Infrastructure.Persistence.Configurations;

public class SettingsConfiguration : IEntityTypeConfiguration<Settings>
{
    public void Configure(EntityTypeBuilder<Settings> builder)
    {
        builder.ToTable(t => t.HasCheckConstraint("CK_Settings_Singleton", "[Id] = 1"));

        builder.Property(s => s.Id)
            .ValueGeneratedNever();

        builder.Property(s => s.SessionPrice)
            .HasPrecision(10, 2);

        builder.Property(s => s.ConsultantWhatsAppNumber)
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(s => s.VodafoneCashNumber)
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(s => s.InstaPayHandle)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(s => s.PaymentInstructions)
            .HasMaxLength(2000);
    }
}
