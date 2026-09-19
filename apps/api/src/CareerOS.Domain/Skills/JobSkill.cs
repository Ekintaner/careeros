using CareerOS.Domain.Common;
using CareerOS.Domain.Opportunities;

namespace CareerOS.Domain.Skills;

public sealed class JobSkill : IAuditedEntity
{
    private JobSkill() { }
    public JobSkill(Job job, Guid skillId, RequirementLevel requirementLevel)
    {
        UserId = job.UserId;
        JobId = job.Id;
        SkillId = Rules.Id(skillId, nameof(skillId));
        SetRequirement(requirementLevel);
    }
    public Guid UserId { get; private set; }
    public Guid JobId { get; private set; }
    public Guid SkillId { get; private set; }
    public RequirementLevel RequirementLevel { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime UpdatedAtUtc { get; private set; }
    public void SetRequirement(RequirementLevel requirementLevel) => RequirementLevel = Rules.EnumValue(requirementLevel);
}
