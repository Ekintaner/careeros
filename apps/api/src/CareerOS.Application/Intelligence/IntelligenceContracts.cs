using CareerOS.Application.Matching;
using CareerOS.Domain.Common;
using CareerOS.Domain.Skills;

namespace CareerOS.Application.Intelligence;

public sealed record IntelligenceQuery(int Page = 1, int PageSize = 20, string State = "active", string Status = "all", string Sort = "priority", string? Search = null)
{
    public void Validate()
    {
        if (Page is < 1 or > 10000 || PageSize is < 1 or > 50) throw new DomainValidationException("Use page 1–10,000 and page size 1–50.");
        if (State is not ("active" or "archived" or "all")) throw new DomainValidationException("Choose active, archived or all jobs.");
        if (Status is not ("all" or "supported" or "evidenceGap" or "missing")) throw new DomainValidationException("Choose a supported evidence status.");
        if (Sort is not ("priority" or "demand" or "name")) throw new DomainValidationException("Choose priority, demand or name sorting.");
        if (Search?.Length > 100) throw new DomainValidationException("Search must be at most 100 characters.");
        if (Search is not null && Search.Trim().Length > 0) _ = new Skill(Search);
    }
}
public sealed record IntelligenceSummary(int TotalJobs, int AnalyzedJobs, int UnassessedJobs, int DistinctSkills, int ProfileSkills,
    int SupportedSkills, int EvidenceGapSkills, int MissingSkills, int EarnedProfilePoints, int PossiblePoints, decimal? ProfileCoverage);
public sealed record IntelligenceSkill(Guid SkillId, string Name, int JobCount, int RequiredJobs, int PreferredJobs, bool InProfile,
    int ConfirmedResumes, string Status, string Action, int WeightedDemand, decimal DemandPercent, decimal ActionPriority, decimal LearningPriority);
public sealed record IntelligenceView(string Algorithm, string State, IntelligenceSummary Summary, IReadOnlyList<IntelligenceSkill> Items,
    int Page, int PageSize, int TotalCount, int TotalPages, bool HistoryAvailable);
public sealed record SkillJob(Guid Id, string Title, string CompanyName, string RequirementLevel, bool Archived,
    int EarnedProfilePoints, int PossiblePoints, decimal? ProfileCoverage);
public sealed record SkillJobsView(Guid SkillId, string Name, IReadOnlyList<SkillJob> Items, int Page, int PageSize, int TotalCount, int TotalPages);
public sealed class IntelligenceNotFoundException : Exception;

public static class IntelligenceRules
{
    public static string Status(bool profile, int resumes) => profile && resumes > 0 ? "supported" : profile || resumes > 0 ? "evidenceGap" : "missing";
    public static string Action(bool profile, int resumes) => profile && resumes > 0 ? "Maintain" : profile ? "Document resume evidence" : resumes > 0 ? "Review profile record" : "Learn or assess";
    public static IntelligenceSkill Skill(Guid id, string name, int required, int preferred, bool profile, int resumes, int analyzedJobs)
    {
        var weighted = required * MatchingRules.RequiredWeight + preferred * MatchingRules.PreferredWeight;
        var priority = MatchingRules.Percentage(weighted, analyzedJobs * MatchingRules.RequiredWeight) ?? 0m;
        var status = Status(profile, resumes);
        return new(id, name, required + preferred, required, preferred, profile, resumes, status, Action(profile, resumes), weighted,
            MatchingRules.Percentage(required + preferred, analyzedJobs) ?? 0m, status == "supported" ? 0m : priority, status == "missing" ? priority : 0m);
    }
}
