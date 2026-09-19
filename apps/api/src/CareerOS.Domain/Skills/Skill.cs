using CareerOS.Domain.Common;

namespace CareerOS.Domain.Skills;

public sealed class Skill : IAuditedEntity
{
    private Skill() { }
    public Skill(string name)
    {
        Id = Guid.NewGuid();
        Rename(name);
    }
    public Guid Id { get; private set; }
    public string Name { get; private set; } = "";
    public string NormalizedName { get; private set; } = "";
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime UpdatedAtUtc { get; private set; }

    public void Rename(string name)
    {
        var validName = Rules.Required(name, 100, nameof(name));
        if (validName.Any(c => c > 127 || char.IsControl(c)))
            throw new DomainValidationException("Canonical skill names currently support printable ASCII; aliases/localization are deferred.");
        Name = validName;
        NormalizedName = Rules.Normalize(Name);
    }
}
