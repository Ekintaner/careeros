using CareerOS.Domain.Opportunities;
using CareerOS.Domain.Skills;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CareerOS.Infrastructure.Persistence.Configurations;

internal sealed class JobSkillConfiguration : IEntityTypeConfiguration<JobSkill>
{
    public void Configure(EntityTypeBuilder<JobSkill> b)
    {
        b.ToTable("job_skills");
        b.HasKey(x => new { x.UserId, x.JobId, x.SkillId });
        Mapping.Owner(b);
        Mapping.Audit(b);
        b.HasOne<Job>().WithMany().HasForeignKey(x => new { x.UserId, x.JobId }).HasPrincipalKey(x => new { x.UserId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Skill>().WithMany().HasForeignKey(x => x.SkillId).OnDelete(DeleteBehavior.Restrict);
        Mapping.Enum<JobSkill, RequirementLevel>(b, nameof(JobSkill.RequirementLevel));
    }
}
