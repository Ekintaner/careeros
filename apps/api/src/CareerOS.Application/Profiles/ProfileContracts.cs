using System.Text.Json.Serialization;
using CareerOS.Application.Authentication;
using CareerOS.Domain.Accounts;
using CareerOS.Domain.Common;
using CareerOS.Domain.Opportunities;
using CareerOS.Domain.Skills;

namespace CareerOS.Application.Profiles;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record ProfileInput(string? Headline, string? Summary, string? CurrentLocation, decimal? YearsOfExperience,
    string? PreferredWorkMode, string[] TargetRoles, string[] PreferredLocations, string[] EmploymentTypes, string[] SeniorityLevels, uint Version)
{
    public void Apply(CareerProfile profile)
    {
        // Validate a detached candidate first: an invalid command cannot partly mutate a tracked aggregate.
        var candidate = new CareerProfile(profile.UserId);
        candidate.Update(Headline, Summary, CurrentLocation, YearsOfExperience, ProfileValidation.OptionalEnum<WorkMode>(PreferredWorkMode));
        candidate.SetPreferences(TargetRoles, PreferredLocations, EmploymentTypes, SeniorityLevels);
        profile.Update(candidate.Headline, candidate.Summary, candidate.CurrentLocation, candidate.YearsOfExperience, candidate.PreferredWorkMode);
        profile.SetPreferences(candidate.TargetRoles, candidate.PreferredLocations, candidate.EmploymentTypes, candidate.SeniorityLevels);
    }
}
public sealed record ProfileView(string? Headline, string? Summary, string? CurrentLocation, decimal? YearsOfExperience,
    string? PreferredWorkMode, string[] TargetRoles, string[] PreferredLocations, string[] EmploymentTypes, string[] SeniorityLevels,
    uint Version, DateTime? UpdatedAtUtc)
{
    public static ProfileView From(CareerProfile p, uint version) => new(p.Headline, p.Summary, p.CurrentLocation, p.YearsOfExperience,
        p.PreferredWorkMode?.ToString(), p.TargetRoles, p.PreferredLocations, p.EmploymentTypes, p.SeniorityLevels, version,
        version == 0 ? null : p.UpdatedAtUtc);
}
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record AddSkillInput(string Name, string? Proficiency, decimal? YearsExperience);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record SkillInput(string? Proficiency, decimal? YearsExperience, uint Version);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record AccountInput(string DisplayName, uint Version);
public sealed record SkillView(Guid SkillId, string Name, string? Proficiency, decimal? YearsExperience, uint Version);
public sealed record SkillSuggestion(Guid Id, string Name);
public sealed record AccountSettingsView(AccountView Account, uint Version);
public sealed class EditConflictException : Exception;
public sealed class ResourceNotFoundException : Exception;
public static class ProfileValidation
{
    public static T? OptionalEnum<T>(string? value) where T : struct, Enum => value is null ? null :
        Enum.GetNames<T>().Contains(value) ? Enum.Parse<T>(value) : throw new DomainValidationException($"Choose a valid {typeof(T).Name}.");
    public static void Assess(UserSkill skill, string? proficiency, decimal? experience) => skill.Assess(OptionalEnum<Proficiency>(proficiency), experience);
}
