using System.Text.Json.Serialization;
using CareerOS.Domain.Common;
using CareerOS.Domain.Skills;

namespace CareerOS.Application.Matching;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record RequirementInput(string Name, string RequirementLevel);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record JobSkillsInput(uint Version, RequirementInput[] Skills);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record ResumeSkillsInput(uint Version, string[] Names);
public sealed record MatchRequirement(Guid SkillId, string Name, string RequirementLevel);
public sealed record SkillEvidence(Guid SkillId, string Name, DateTime RecordedAtUtc);
public sealed record MatchRow(Guid SkillId, string Name, string RequirementLevel, int Weight, int EarnedPoints, bool Matched, DateTime? EvidenceRecordedAtUtc);
public sealed record MatchBreakdown(int Matched, int Total, int EarnedPoints, int PossiblePoints);
public sealed record MatchResult(string Algorithm, decimal? Score, MatchBreakdown Required, MatchBreakdown Preferred, MatchRow[] Skills);
public sealed record ResumeSkillsView(Guid Id, string Name, uint Version, SkillEvidence[] Skills);
public sealed record MatchingView(Guid JobId, uint JobVersion, string Source, ResumeSkillsView? Resume, MatchResult Result);
public sealed class MatchingNotFoundException : Exception;
public sealed class MatchingConflictException : Exception;

public static class MatchingRules
{
    public const int MaxSkills = 100;
    public const int RequiredWeight = 3;
    public const int PreferredWeight = 1;
    public static decimal? Percentage(int earned, int possible) => possible == 0 ? null :
        decimal.Round(100m * earned / possible, 1, MidpointRounding.AwayFromZero);
    public static RequirementInput[] Validate(JobSkillsInput input)
    {
        if (input.Version == 0 || input.Skills is null || input.Skills.Length > MaxSkills)
            throw new DomainValidationException("Provide a version and at most 100 job skills.");
        var seen = new HashSet<string>(StringComparer.Ordinal);
        return input.Skills.Select(x =>
        {
            if (x is null) throw new DomainValidationException("A skill is required.");
            var skill = new Skill(x.Name);
            if (!seen.Add(skill.NormalizedName)) throw new DomainValidationException("Duplicate skills are not allowed.");
            if (x.RequirementLevel is not ("Required" or "Preferred")) throw new DomainValidationException("Choose Required or Preferred.");
            return new RequirementInput(skill.Name, x.RequirementLevel);
        }).ToArray();
    }
    public static string[] Validate(ResumeSkillsInput input)
    {
        if (input.Version == 0 || input.Names is null || input.Names.Length > MaxSkills)
            throw new DomainValidationException("Provide a version and at most 100 confirmed resume skills.");
        var skills = input.Names.Select(x => new Skill(x)).ToArray();
        if (skills.Select(x => x.NormalizedName).Distinct(StringComparer.Ordinal).Count() != skills.Length)
            throw new DomainValidationException("Duplicate skills are not allowed.");
        return skills.Select(x => x.Name).ToArray();
    }
    public static MatchResult Calculate(IEnumerable<MatchRequirement> requirements, IEnumerable<SkillEvidence> evidence)
    {
        var known = evidence.ToDictionary(x => x.SkillId);
        var rows = requirements.OrderBy(x => x.RequirementLevel == "Required" ? 0 : 1)
            .ThenBy(x => x.Name, StringComparer.OrdinalIgnoreCase).ThenBy(x => x.SkillId)
            .Select(x =>
            {
                if (x.RequirementLevel is not ("Required" or "Preferred")) throw new DomainValidationException("Invalid requirement level.");
                var matched = known.TryGetValue(x.SkillId, out var e);
                var weight = x.RequirementLevel == "Required" ? RequiredWeight : PreferredWeight;
                return new MatchRow(x.SkillId, x.Name, x.RequirementLevel, weight, matched ? weight : 0, matched, e?.RecordedAtUtc);
            }).ToArray();
        MatchBreakdown Part(string level)
        {
            var part = rows.Where(x => x.RequirementLevel == level).ToArray();
            return new(part.Count(x => x.Matched), part.Length, part.Sum(x => x.EarnedPoints), part.Sum(x => x.Weight));
        }
        var possible = rows.Sum(x => x.Weight);
        return new("skills-v1", Percentage(rows.Sum(x => x.EarnedPoints), possible),
            Part("Required"), Part("Preferred"), rows);
    }
}
