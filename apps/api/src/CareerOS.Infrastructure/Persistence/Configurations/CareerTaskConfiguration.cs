using CareerOS.Domain.Applications;
using CareerOS.Domain.Planning;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CareerOS.Infrastructure.Persistence.Configurations;

internal sealed class CareerTaskConfiguration : IEntityTypeConfiguration<CareerTask>
{
    public void Configure(EntityTypeBuilder<CareerTask> b)
    {
        b.ToTable("career_tasks");
        Mapping.Identity(b);
        Mapping.Owner(b);
        Mapping.Audit(b);
        b.HasOne<JobApplication>().WithMany().HasForeignKey(x => new { x.UserId, x.ApplicationId }).HasPrincipalKey(x => new { x.UserId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        Mapping.Text(b, nameof(CareerTask.Title), 200, true);
        Mapping.Text(b, nameof(CareerTask.Description), 10000);
        Mapping.Enum<CareerTask, TaskPriority>(b, nameof(CareerTask.Priority));
        b.Property(x => x.DueAtUtc).HasColumnType("timestamp with time zone");
        b.Property(x => x.CompletedAtUtc).HasColumnType("timestamp with time zone");
        Mapping.Check(b, "completion_order", "\"CompletedAtUtc\" >= \"CreatedAtUtc\"");
        b.HasIndex(x => new { x.UserId, x.DueAtUtc });
        b.HasIndex(x => new { x.UserId, x.CompletedAtUtc });
    }
}
