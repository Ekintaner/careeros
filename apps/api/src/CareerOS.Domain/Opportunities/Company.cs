using CareerOS.Domain.Common;

namespace CareerOS.Domain.Opportunities;

public sealed class Company : IAuditedEntity
{
    private Company() { }
    public Company(Guid userId, string name)
    {
        Id = Guid.NewGuid();
        UserId = Rules.Id(userId, nameof(userId));
        Rename(name);
    }
    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public string Name { get; private set; } = "";
    public string? Website { get; private set; }
    public string? Industry { get; private set; }
    public string? Location { get; private set; }
    public string? Description { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime UpdatedAtUtc { get; private set; }

    public void Rename(string name) => Name = Rules.Required(name, 200, nameof(name));
    public void UpdateDetails(string? website, string? industry, string? location, string? description)
    {
        var validUrl = Rules.Url(website);
        var validIndustry = Rules.Optional(industry, 120, nameof(industry));
        var validLocation = Rules.Optional(location, 200, nameof(location));
        var validDescription = Rules.Optional(description, 10000, nameof(description));
        Website = validUrl;
        Industry = validIndustry;
        Location = validLocation;
        Description = validDescription;
    }
}
