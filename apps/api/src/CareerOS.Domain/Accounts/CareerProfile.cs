using CareerOS.Domain.Common;
using CareerOS.Domain.Opportunities;

namespace CareerOS.Domain.Accounts;

public sealed class CareerProfile : IAuditedEntity
{
    private CareerProfile() { }
    public CareerProfile(Guid userId) => UserId = Rules.Id(userId, nameof(userId));
    public Guid UserId { get; private set; }
    public string? Headline { get; private set; }
    public string? Summary { get; private set; }
    public string? CurrentLocation { get; private set; }
    public decimal? YearsOfExperience { get; private set; }
    public WorkMode? PreferredWorkMode { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime UpdatedAtUtc { get; private set; }

    public void Update(string? headline, string? summary, string? currentLocation, decimal? yearsOfExperience, WorkMode? preferredWorkMode)
    {
        var validHeadline = Rules.Optional(headline, 200, nameof(headline));
        var validSummary = Rules.Optional(summary, 10000, nameof(summary));
        var validLocation = Rules.Optional(currentLocation, 200, nameof(currentLocation));
        var validExperience = Rules.Experience(yearsOfExperience);
        if (preferredWorkMode.HasValue) Rules.EnumValue(preferredWorkMode.Value);
        Headline = validHeadline;
        Summary = validSummary;
        CurrentLocation = validLocation;
        YearsOfExperience = validExperience;
        PreferredWorkMode = preferredWorkMode;
    }
}
