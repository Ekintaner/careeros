using CareerOS.Domain.Applications;
using CareerOS.Domain.Planning;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CareerOS.Infrastructure.Persistence.Configurations;

internal sealed class InterviewConfiguration : IEntityTypeConfiguration<Interview>
{
    public void Configure(EntityTypeBuilder<Interview> b)
    {
        b.ToTable("interviews");
        Mapping.Identity(b);
        Mapping.Owner(b, true);
        Mapping.Audit(b);
        b.HasOne<JobApplication>().WithMany().HasForeignKey(x => new { x.UserId, x.ApplicationId }).HasPrincipalKey(x => new { x.UserId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        Mapping.Enum<Interview, InterviewType>(b, nameof(Interview.Type));
        b.Property(x => x.ScheduledStartUtc).HasColumnType("timestamp with time zone").IsRequired();
        b.Property(x => x.ScheduledEndUtc).HasColumnType("timestamp with time zone").IsRequired();
        Mapping.Check(b, "schedule_order", "\"ScheduledEndUtc\" > \"ScheduledStartUtc\"");
        Mapping.Text(b, nameof(Interview.Location), 300);
        Mapping.Url(b, nameof(Interview.MeetingUrl));
        Mapping.Text(b, nameof(Interview.Outcome), 2000);
        b.HasIndex(x => new { x.UserId, x.ScheduledStartUtc });
    }
}
