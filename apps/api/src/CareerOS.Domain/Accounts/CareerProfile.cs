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
    public string[] TargetRoles { get; private set; } = [];
    public string[] PreferredLocations { get; private set; } = [];
    public string[] EmploymentTypes { get; private set; } = [];
    public string[] SeniorityLevels { get; private set; } = [];
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime UpdatedAtUtc { get; private set; }

    public void SetPreferences(string[]? targetRoles, string[]? locations, string[]? employmentTypes, string[]? seniorityLevels)
    {
        var roles = TextPreferences(targetRoles, 100, "Target roles");
        var places = TextPreferences(locations, 200, "Preferred locations");
        var employment = EnumPreferences<EmploymentType>(employmentTypes);
        var seniority = EnumPreferences<SeniorityLevel>(seniorityLevels);
        TargetRoles = roles;
        PreferredLocations = places;
        EmploymentTypes = employment;
        SeniorityLevels = seniority;
    }

    private static string[] TextPreferences(string[]? values, int length, string name)
    {
        if (values is null || values.Length > 10)
            throw new DomainValidationException($"{name} must be an array with at most 10 entries.");
        return values.Select(v => Rules.Required(v, length, name)).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
    }

    private static string[] EnumPreferences<T>(string[]? values) where T : struct, Enum
    {
        if (values is null || values.Length > Enum.GetNames<T>().Length || values.Any(v => !Enum.GetNames<T>().Contains(v)))
            throw new DomainValidationException($"Choose valid {typeof(T).Name} preferences.");
        return values.Distinct().Order().ToArray();
    }

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
