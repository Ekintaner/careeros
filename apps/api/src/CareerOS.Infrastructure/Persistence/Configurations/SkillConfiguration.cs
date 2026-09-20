using CareerOS.Domain.Skills;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CareerOS.Infrastructure.Persistence.Configurations;

internal sealed class SkillConfiguration : IEntityTypeConfiguration<Skill>
{
    public void Configure(EntityTypeBuilder<Skill> b)
    {
        b.ToTable("skills");
        Mapping.Identity(b);
        Mapping.Audit(b);
        Mapping.Text(b, nameof(Skill.Name), 100, true);
        b.Property(x => x.NormalizedName).HasMaxLength(100).HasComputedColumnSql("translate(btrim(\"Name\"), 'ABCDEFGHIJKLMNOPQRSTUVWXYZ', 'abcdefghijklmnopqrstuvwxyz')", stored: true);
        Mapping.Check(b, "name_canonical", "\"Name\" = btrim(\"Name\") AND \"Name\" ~ '^[ -~]+$'");
        b.HasIndex(x => x.NormalizedName).IsUnique();
        b.HasIndex(x => x.NormalizedName, "IX_skills_NormalizedName_prefix").HasOperators("varchar_pattern_ops");
    }
}
