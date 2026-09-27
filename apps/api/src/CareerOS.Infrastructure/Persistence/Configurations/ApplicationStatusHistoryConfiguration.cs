using CareerOS.Domain.Applications;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CareerOS.Infrastructure.Persistence.Configurations;

internal sealed class ApplicationStatusHistoryConfiguration : IEntityTypeConfiguration<ApplicationStatusHistory>
{
    public void Configure(EntityTypeBuilder<ApplicationStatusHistory> b)
    {
        b.ToTable("application_status_history");
        Mapping.Identity(b);
        Mapping.Owner(b);
        Mapping.Enum<ApplicationStatusHistory, ApplicationStatus>(b, nameof(ApplicationStatusHistory.FromStatus), true);
        Mapping.Enum<ApplicationStatusHistory, ApplicationStatus>(b, nameof(ApplicationStatusHistory.ToStatus));
        b.Property(x => x.ChangedAtUtc).HasColumnType("timestamp with time zone").IsRequired();
        Mapping.Check(b, "initial_or_transition", "(\"Sequence\" = 0 AND \"FromStatus\" IS NULL AND \"ToStatus\" IN ('Saved','Applied')) OR (\"Sequence\" > 0 AND \"FromStatus\" IS NOT NULL AND \"FromStatus\" <> \"ToStatus\")");
        b.HasIndex(x => new { x.ApplicationId, x.Sequence }).IsUnique();
        b.HasIndex(x => new { x.ApplicationId, x.ChangedAtUtc, x.Sequence });
    }
}
