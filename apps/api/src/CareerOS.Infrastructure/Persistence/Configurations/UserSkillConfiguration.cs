using CareerOS.Domain.Skills;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CareerOS.Infrastructure.Persistence.Configurations;

internal sealed class UserSkillConfiguration : IEntityTypeConfiguration<UserSkill>
{
    public void Configure(EntityTypeBuilder<UserSkill> b)
    {
        b.ToTable("user_skills");
        b.HasKey(x => new { x.UserId, x.SkillId });
        Mapping.Owner(b);
        Mapping.Audit(b);
        b.HasOne<Skill>().WithMany().HasForeignKey(x => x.SkillId).OnDelete(DeleteBehavior.Restrict);
        Mapping.Enum<UserSkill, Proficiency>(b, nameof(UserSkill.Proficiency), true);
        b.Property(x => x.YearsExperience).HasPrecision(4, 1);
        Mapping.Check(b, "experience_range", "\"YearsExperience\" BETWEEN 0 AND 100");
    }
}
