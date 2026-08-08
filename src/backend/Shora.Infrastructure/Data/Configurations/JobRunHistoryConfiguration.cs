using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Shora.Domain.Entities;

namespace Shora.Infrastructure.Data.Configurations;

public class JobRunHistoryConfiguration : IEntityTypeConfiguration<JobRunHistory>
{
    public void Configure(EntityTypeBuilder<JobRunHistory> builder)
    {
        builder.HasKey(j => j.JobName);

        builder.Property(j => j.JobName)
            .HasMaxLength(128);

        builder.Property(j => j.LastError)
            .HasMaxLength(2000);
    }
}
