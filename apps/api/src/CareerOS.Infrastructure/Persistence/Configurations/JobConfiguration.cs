using CareerOS.Domain.Opportunities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CareerOS.Infrastructure.Persistence.Configurations;

internal sealed class JobConfiguration : IEntityTypeConfiguration<Job>
{
    public void Configure(EntityTypeBuilder<Job> b)
    {
        b.ToTable("jobs");
        Mapping.Identity(b);
        Mapping.Owner(b, true);
        Mapping.Audit(b);
        b.HasOne<Company>().WithMany().HasForeignKey(x => new { x.UserId, x.CompanyId }).HasPrincipalKey(x => new { x.UserId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        Mapping.Text(b, nameof(Job.Title), 200, true);
        Mapping.Text(b, nameof(Job.Description), 50000);
        Mapping.Text(b, nameof(Job.Location), 200);
        Mapping.Text(b, nameof(Job.Source), 120);
        Mapping.Url(b, nameof(Job.SourceUrl));
        Mapping.Enum<Job, WorkMode>(b, nameof(Job.WorkMode), true);
        Mapping.Enum<Job, EmploymentType>(b, nameof(Job.EmploymentType), true);
        Mapping.Enum<Job, SeniorityLevel>(b, nameof(Job.SeniorityLevel), true);
        Mapping.Enum<Job, SalaryPeriod>(b, nameof(Job.SalaryPeriod), true);
        b.Property(x => x.SalaryMin).HasPrecision(14, 2);
        b.Property(x => x.SalaryMax).HasPrecision(14, 2);
        b.Property(x => x.SalaryCurrency).HasMaxLength(3);
        Mapping.Check(b, "salary_range", "(\"SalaryMin\" IS NULL OR \"SalaryMin\" >= 0) AND (\"SalaryMax\" IS NULL OR \"SalaryMax\" >= 0) AND (\"SalaryMin\" IS NULL OR \"SalaryMax\" IS NULL OR \"SalaryMin\" <= \"SalaryMax\")");
        Mapping.Check(b, "salary_metadata", "((\"SalaryMin\" IS NOT NULL OR \"SalaryMax\" IS NOT NULL) AND \"SalaryCurrency\" IS NOT NULL AND \"SalaryCurrency\" ~ '^[A-Z]{3}$' AND \"SalaryPeriod\" IS NOT NULL) OR (\"SalaryMin\" IS NULL AND \"SalaryMax\" IS NULL AND \"SalaryCurrency\" IS NULL AND \"SalaryPeriod\" IS NULL)");
        b.Property(x => x.PostedAtUtc).HasColumnType("timestamp with time zone");
        b.Property(x => x.ExpiresAtUtc).HasColumnType("timestamp with time zone");
        Mapping.Check(b, "posting_order", "\"ExpiresAtUtc\" >= \"PostedAtUtc\"");
        b.HasIndex(x => new { x.UserId, x.CreatedAtUtc });
        b.HasIndex(x => new { x.UserId, x.PostedAtUtc });
    }
}
