using CareerOS.Domain.Applications;
using CareerOS.Domain.Documents;
using CareerOS.Domain.Opportunities;
using CareerOS.Domain.Planning;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CareerOS.Infrastructure.Persistence.Configurations;

internal sealed class JobApplicationConfiguration : IEntityTypeConfiguration<JobApplication>
{
    public void Configure(EntityTypeBuilder<JobApplication> b)
    {
        b.ToTable("job_applications");
        Mapping.Identity(b);
        Mapping.Owner(b, true);
        Mapping.Audit(b);
        b.HasOne<Job>().WithMany().HasForeignKey(x => new { x.UserId, x.JobId }).HasPrincipalKey(x => new { x.UserId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Resume>().WithMany().HasForeignKey(x => new { x.UserId, x.ResumeId }).HasPrincipalKey(x => new { x.UserId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        Mapping.Enum<JobApplication, ApplicationStatus>(b, nameof(JobApplication.Status));
        Mapping.Text(b, nameof(JobApplication.Source), 120);
        b.Property(x => x.AppliedAtUtc).HasColumnType("timestamp with time zone");
        b.Property(x => x.LastStatusChangedAtUtc).HasColumnType("timestamp with time zone").IsRequired();
        Mapping.Check(b, "revision", "\"StatusRevision\" >= 0");
        Mapping.Check(b, "applied_order", "\"AppliedAtUtc\" <= \"LastStatusChangedAtUtc\"");
        Mapping.Check(b, "applied_required", "\"Status\" NOT IN ('Applied', 'Assessment', 'Interview', 'Offer', 'Rejected', 'Accepted') OR \"AppliedAtUtc\" IS NOT NULL");
        b.HasIndex(x => new { x.UserId, x.JobId }).IsUnique();
        b.HasIndex(x => new { x.UserId, x.Status, x.CreatedAtUtc });
        b.HasIndex(x => new { x.UserId, x.CreatedAtUtc });
        b.HasMany(x => x.StatusHistory).WithOne().HasForeignKey(x => new { x.UserId, x.ApplicationId }).HasPrincipalKey(x => new { x.UserId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        b.Navigation(x => x.StatusHistory).HasField("_statusHistory").UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
