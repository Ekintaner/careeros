using System.Data;
using CareerOS.Application.Intelligence;
using CareerOS.Application.Matching;
using CareerOS.Domain.Opportunities;
using CareerOS.Domain.Skills;
using CareerOS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CareerOS.Infrastructure.Intelligence;

public sealed class IntelligenceService(CareerOSDbContext db, OwnedData owned)
{
    private IQueryable<Job> Jobs(string state) => state == "all" ? owned.Query<Job>() : state == "archived"
        ? owned.Query<Job>().Where(x => x.ArchivedAtUtc != null) : owned.Query<Job>().Where(x => x.ArchivedAtUtc == null);
    private sealed class Demand
    {
        public Guid SkillId { get; init; }
        public string Name { get; init; } = "";
        public string NormalizedName { get; init; } = "";
        public int Required { get; init; }
        public int Preferred { get; init; }
        public bool Profile { get; init; }
        public int Resumes { get; init; }
    }
    private IQueryable<Demand> Demands(IQueryable<Guid> jobIds)
    {
        var grouped = from r in owned.Query<JobSkill>()
                      where jobIds.Contains(r.JobId)
                      group r by r.SkillId into g
                      select new { SkillId = g.Key, Required = g.Count(x => x.RequirementLevel == RequirementLevel.Required), Preferred = g.Count(x => x.RequirementLevel == RequirementLevel.Preferred) };
        var resumeCounts = from r in owned.Query<ResumeSkill>() group r by r.SkillId into g select new { SkillId = g.Key, Count = g.Count() };
        return from d in grouped
               join s in db.Skills on d.SkillId equals s.Id
               join p in owned.Query<UserSkill>() on d.SkillId equals p.SkillId into profiles
               from p in profiles.DefaultIfEmpty()
               join r in resumeCounts on d.SkillId equals r.SkillId into resumes
               from r in resumes.DefaultIfEmpty()
               select new Demand
               {
                   SkillId = d.SkillId,
                   Name = s.Name,
                   NormalizedName = s.NormalizedName,
                   Required = d.Required,
                   Preferred = d.Preferred,
                   Profile = p != null,
                   Resumes = (int?)r.Count ?? 0
               };
    }
    public async Task<IntelligenceView> Get(IntelligenceQuery query, CancellationToken ct)
    {
        query.Validate();
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.RepeatableRead, ct);
        var jobs = Jobs(query.State);
        var jobStats = await jobs.GroupBy(x => 1).Select(g => new { Total = g.Count(), Analyzed = g.Count(j => owned.Query<JobSkill>().Any(r => r.JobId == j.Id)) }).SingleOrDefaultAsync(ct);
        var demands = Demands(jobs.Select(x => x.Id));
        var stats = await demands.GroupBy(x => 1).Select(g => new
        {
            Total = g.Count(),
            Profile = g.Count(x => x.Profile),
            Supported = g.Count(x => x.Profile && x.Resumes > 0),
            EvidenceGap = g.Count(x => (x.Profile && x.Resumes == 0) || (!x.Profile && x.Resumes > 0)),
            Missing = g.Count(x => !x.Profile && x.Resumes == 0),
            Possible = g.Sum(x => x.Required * MatchingRules.RequiredWeight + x.Preferred * MatchingRules.PreferredWeight),
            Earned = g.Sum(x => x.Profile ? x.Required * MatchingRules.RequiredWeight + x.Preferred * MatchingRules.PreferredWeight : 0)
        }).SingleOrDefaultAsync(ct);
        var filtered = query.Status switch
        {
            "supported" => demands.Where(x => x.Profile && x.Resumes > 0),
            "evidenceGap" => demands.Where(x => (x.Profile && x.Resumes == 0) || (!x.Profile && x.Resumes > 0)),
            "missing" => demands.Where(x => !x.Profile && x.Resumes == 0),
            _ => demands
        };
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var normalized = new Skill(query.Search).NormalizedName;
            filtered = filtered.Where(x => x.NormalizedName.Contains(normalized));
        }
        var count = await filtered.CountAsync(ct);
        var ordered = query.Sort switch
        {
            "demand" => filtered.OrderByDescending(x => x.Required + x.Preferred).ThenByDescending(x => x.Required).ThenBy(x => x.NormalizedName).ThenBy(x => x.SkillId),
            "name" => filtered.OrderBy(x => x.NormalizedName).ThenBy(x => x.SkillId),
            _ => filtered.OrderByDescending(x => x.Profile && x.Resumes > 0 ? 0 : x.Required * MatchingRules.RequiredWeight + x.Preferred * MatchingRules.PreferredWeight)
                .ThenBy(x => x.Profile || x.Resumes > 0).ThenByDescending(x => x.Required + x.Preferred).ThenBy(x => x.NormalizedName).ThenBy(x => x.SkillId)
        };
        var rows = await ordered.Skip((query.Page - 1) * query.PageSize).Take(query.PageSize).ToArrayAsync(ct);
        var totalJobs = jobStats?.Total ?? 0; var analyzed = jobStats?.Analyzed ?? 0;
        var summary = new IntelligenceSummary(totalJobs, analyzed, totalJobs - analyzed, stats?.Total ?? 0, stats?.Profile ?? 0, stats?.Supported ?? 0,
            stats?.EvidenceGap ?? 0, stats?.Missing ?? 0, stats?.Earned ?? 0, stats?.Possible ?? 0, MatchingRules.Percentage(stats?.Earned ?? 0, stats?.Possible ?? 0));
        var result = new IntelligenceView("skills-intelligence-v1 / skills-v1", query.State, summary,
            rows.Select(x => IntelligenceRules.Skill(x.SkillId, x.Name, x.Required, x.Preferred, x.Profile, x.Resumes, analyzed)).ToArray(),
            query.Page, query.PageSize, count, (int)Math.Ceiling(count / (double)query.PageSize), false);
        await tx.CommitAsync(ct);
        return result;
    }
    public async Task<SkillJobsView> SkillJobs(Guid skillId, IntelligenceQuery query, CancellationToken ct)
    {
        query.Validate();
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.RepeatableRead, ct);
        var jobs = Jobs(query.State);
        var requirements = owned.Query<JobSkill>();
        var name = await (from r in requirements
                          join j in jobs on r.JobId equals j.Id
                          join s in db.Skills on r.SkillId equals s.Id
                          where r.SkillId == skillId
                          select s.Name).FirstOrDefaultAsync(ct) ?? throw new IntelligenceNotFoundException();
        var rows = from r in requirements
                   join j in jobs on r.JobId equals j.Id
                   join c in owned.Query<Company>() on j.CompanyId equals c.Id
                   where r.SkillId == skillId
                   select new { j.Id, j.Title, CompanyName = c.Name, r.RequirementLevel, Archived = j.ArchivedAtUtc != null };
        var count = await rows.CountAsync(ct);
        var scores = from r in requirements
                     join p in owned.Query<UserSkill>() on r.SkillId equals p.SkillId into profile
                     from p in profile.DefaultIfEmpty()
                     group new { r, p } by r.JobId into g
                     select new
                     {
                         JobId = g.Key,
                         Possible = g.Sum(x => x.r.RequirementLevel == RequirementLevel.Required ? MatchingRules.RequiredWeight : MatchingRules.PreferredWeight),
                         Earned = g.Sum(x => x.p != null ? (x.r.RequirementLevel == RequirementLevel.Required ? MatchingRules.RequiredWeight : MatchingRules.PreferredWeight) : 0)
                     };
        var page = await (from j in rows
                          join s in scores on j.Id equals s.JobId
                          orderby j.Title, j.Id
                          select new { j.Id, j.Title, j.CompanyName, Level = j.RequirementLevel.ToString(), j.Archived, s.Earned, s.Possible })
            .Skip((query.Page - 1) * query.PageSize).Take(query.PageSize).ToArrayAsync(ct);
        var result = new SkillJobsView(skillId, name, page.Select(x => new SkillJob(x.Id, x.Title, x.CompanyName, x.Level, x.Archived, x.Earned, x.Possible, MatchingRules.Percentage(x.Earned, x.Possible))).ToArray(),
            query.Page, query.PageSize, count, (int)Math.Ceiling(count / (double)query.PageSize));
        await tx.CommitAsync(ct);
        return result;
    }
}
