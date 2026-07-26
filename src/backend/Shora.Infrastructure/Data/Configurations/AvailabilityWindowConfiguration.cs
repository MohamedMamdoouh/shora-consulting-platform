using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Shora.Domain.Entities;

namespace Shora.Infrastructure.Data.Configurations;

public class AvailabilityWindowConfiguration : IEntityTypeConfiguration<AvailabilityWindow>
{
    public void Configure(EntityTypeBuilder<AvailabilityWindow> builder)
    {
        builder.Property(w => w.DayOfWeek)
            .HasConversion<int>();
    }
}
