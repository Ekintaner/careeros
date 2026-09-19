using CareerOS.Domain.Documents;
using CareerOS.Domain.Skills;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CareerOS.Infrastructure.Persistence.Configurations;

internal sealed class ResumeSkillConfiguration : IEntityTypeConfiguration<ResumeSkill>
{
    public void Configure(EntityTypeBuilder<ResumeSkill> b)
    {
        b.ToTable("resume_skills");
        b.HasKey(x => new { x.UserId, x.ResumeId, x.SkillId });
        Mapping.Owner(b);
        Mapping.Audit(b);
        b.HasOne<Resume>().WithMany().HasForeignKey(x => new { x.UserId, x.ResumeId }).HasPrincipalKey(x => new { x.UserId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Skill>().WithMany().HasForeignKey(x => x.SkillId).OnDelete(DeleteBehavior.Restrict);
    }
}
