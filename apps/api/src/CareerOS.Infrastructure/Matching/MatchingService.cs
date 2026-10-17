using CareerOS.Application.Authentication;
using System.Data;
using CareerOS.Application.Matching;
using CareerOS.Domain.Documents;
using CareerOS.Domain.Opportunities;
using CareerOS.Domain.Skills;
using CareerOS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CareerOS.Infrastructure.Matching;

public sealed class MatchingService(CareerOSDbContext db, OwnedData owned, TimeProvider clock, ICurrentUser current)
{
    private uint Version(object entity) => (uint)db.Entry(entity).Property("Version").CurrentValue!;
    private void Touch(object entity, uint version)
    {
        if (Version(entity) != version) throw new MatchingConflictException();
        db.Entry(entity).Property("Version").OriginalValue = version;
        // Even a same-value replacement advances xmin, protecting the complete skill set from stale editors.
        db.Entry(entity).Property("UpdatedAtUtc").IsModified = true;
    }
    public async Task<MatchingView> Get(Guid jobId, Guid? resumeId, CancellationToken ct)
    {
        // One consistent snapshot for requirements, selected source and explanations. No persisted score can become stale.
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.RepeatableRead, ct);
        var jobVersion = await owned.Query<Job>().AsNoTracking().Where(x => x.Id == jobId)
            .Select(x => (uint?)EF.Property<uint>(x, "Version")).SingleOrDefaultAsync(ct) ?? throw new MatchingNotFoundException();
        var requirements = await (from r in owned.Query<JobSkill>() where r.JobId == jobId
                                  join s in db.Skills on r.SkillId equals s.Id
                                  select new MatchRequirement(s.Id, s.Name, r.RequirementLevel.ToString())).ToArrayAsync(ct);
        ResumeSkillsView? resume = null;
        SkillEvidence[] evidence;
        if (resumeId.HasValue)
        {
            resume = await Resume(resumeId.Value, ct);
            evidence = resume.Skills;
        }
        else
        {
            var ids = requirements.Select(x => x.SkillId).ToArray();
            evidence = await (from r in owned.Query<UserSkill>() where ids.Contains(r.SkillId)
                              join s in db.Skills on r.SkillId equals s.Id
                              select new SkillEvidence(s.Id, s.Name, r.UpdatedAtUtc)).ToArrayAsync(ct);
        }
        var result = new MatchingView(jobId, jobVersion, resume is null ? "Profile" : "Resume", resume, MatchingRules.Calculate(requirements, evidence));
        await tx.CommitAsync(ct);
        return result;
    }
    public async Task<ResumeSkillsView> Resume(Guid id, CancellationToken ct)
    {
        var resume = await owned.Query<Resume>().AsNoTracking().Where(x => x.Id == id)
            .Select(x => new { x.Id, x.Name, Version = EF.Property<uint>(x, "Version") }).SingleOrDefaultAsync(ct) ?? throw new MatchingNotFoundException();
        var skills = await (from r in owned.Query<ResumeSkill>() where r.ResumeId == id
                            join s in db.Skills on r.SkillId equals s.Id orderby s.NormalizedName
                            select new SkillEvidence(s.Id, s.Name, r.CreatedAtUtc)).ToArrayAsync(ct);
        return new(id, resume.Name, resume.Version, skills);
    }
    private async Task<Skill> Canonical(string name, CancellationToken ct)
    {
        var skill = new Skill(name);
        var now = clock.GetUtcNow().UtcDateTime;
        // Shared vocabulary only; this does not assert a user/profile/resume skill.
        await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO skills (\"Id\", \"Name\", \"CreatedAtUtc\", \"UpdatedAtUtc\") VALUES ({skill.Id}, {skill.Name}, {now}, {now}) ON CONFLICT (\"NormalizedName\") DO NOTHING", ct);
        return await db.Skills.AsNoTracking().SingleAsync(x => x.NormalizedName == skill.NormalizedName, ct);
    }
    public async Task SaveJob(Guid id, JobSkillsInput input, CancellationToken ct)
    {
        var skills = MatchingRules.Validate(input);
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var job = await owned.Query<Job>().SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw new MatchingNotFoundException();
        Touch(job, input.Version);
        // Acquire the parent's concurrency check/row lock before touching associations or shared vocabulary.
        await owned.SaveChangesAsync(ct);
        var existing = await owned.Query<JobSkill>().Where(x => x.JobId == id).ToListAsync(ct);
        var keep = new HashSet<Guid>();
        // Sort insertion order to avoid deadlocks between shared vocabulary writers.
        foreach (var item in skills.OrderBy(x => new Skill(x.Name).NormalizedName, StringComparer.Ordinal))
        {
            var canonical = await Canonical(item.Name, ct);
            keep.Add(canonical.Id);
            var relation = existing.SingleOrDefault(x => x.SkillId == canonical.Id);
            var level = Enum.Parse<RequirementLevel>(item.RequirementLevel);
            if (relation is null) owned.Add(new JobSkill(job, canonical.Id, level));
            else relation.SetRequirement(level);
        }
        foreach (var relation in existing.Where(x => !keep.Contains(x.SkillId))) owned.Remove(relation);
        await owned.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
    }
    public async Task SaveResume(Guid id, ResumeSkillsInput input, CancellationToken ct)
    {
        var names = MatchingRules.Validate(input);
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        // Share document lifecycle lock order: account advisory lock, parent row, then skill rows.
        await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(hashtextextended({current.UserId.ToString()}, 7))", ct);
        var resume = await owned.Query<Resume>().SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw new MatchingNotFoundException();
        Touch(resume, input.Version);
        await owned.SaveChangesAsync(ct);
        var existing = await owned.Query<ResumeSkill>().Where(x => x.ResumeId == id).ToListAsync(ct);
        var keep = new HashSet<Guid>();
        foreach (var name in names.OrderBy(x => new Skill(x).NormalizedName, StringComparer.Ordinal))
        {
            var canonical = await Canonical(name, ct);
            keep.Add(canonical.Id);
            if (existing.All(x => x.SkillId != canonical.Id)) owned.Add(new ResumeSkill(resume, canonical.Id));
        }
        foreach (var relation in existing.Where(x => !keep.Contains(x.SkillId))) owned.Remove(relation);
        await owned.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
    }
}
