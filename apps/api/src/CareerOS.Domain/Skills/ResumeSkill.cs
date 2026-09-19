using CareerOS.Domain.Common;
using CareerOS.Domain.Documents;

namespace CareerOS.Domain.Skills;

public sealed class ResumeSkill : ICreatedEntity
{
    private ResumeSkill() { }
    public ResumeSkill(Resume resume, Guid skillId)
    {
        UserId = resume.UserId;
        ResumeId = resume.Id;
        SkillId = Rules.Id(skillId, nameof(skillId));
    }
    public Guid UserId { get; private set; }
    public Guid ResumeId { get; private set; }
    public Guid SkillId { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
}
