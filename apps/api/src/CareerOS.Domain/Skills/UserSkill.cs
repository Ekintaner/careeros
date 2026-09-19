using CareerOS.Domain.Common;

namespace CareerOS.Domain.Skills;

public enum Proficiency { Beginner, Intermediate, Advanced, Expert }
public enum RequirementLevel { Required, Preferred }

public sealed class UserSkill : IAuditedEntity
{
    private UserSkill() { }
    public UserSkill(Guid userId, Guid skillId)
    {
        UserId = Rules.Id(userId, nameof(userId));
        SkillId = Rules.Id(skillId, nameof(skillId));
    }
    public Guid UserId { get; private set; }
    public Guid SkillId { get; private set; }
    public Proficiency? Proficiency { get; private set; }
    public decimal? YearsExperience { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime UpdatedAtUtc { get; private set; }

    public void Assess(Proficiency? proficiency, decimal? yearsExperience)
    {
        if (proficiency.HasValue) Rules.EnumValue(proficiency.Value);
        var experience = Rules.Experience(yearsExperience);
        Proficiency = proficiency;
        YearsExperience = experience;
    }
}
