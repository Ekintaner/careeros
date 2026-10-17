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
        var requirements = await (from r in owned.Query<JobSkill>()
                                  where r.JobId == jobId
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
            evidence = await (from r in owned.Query<UserSkill>()
                              where ids.Contains(r.SkillId)
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
        var skills = await (from r in owned.Query<ResumeSkill>()
                            where r.ResumeId == id
                            join s in db.Skills on r.SkillId equals s.Id
                            orderby s.NormalizedName
                            select new SkillEvidence(s.Id, s.Name, r.CreatedAtUtc)).ToArrayAsync(ct);
        return new(id, resume.Name, resume.Version, skills);
    }
    private async Task<Dictionary<string, Skill>> Canonical(IEnumerable<string> names, CancellationToken ct)
    {
        var skills = names.Select(name => new Skill(name)).OrderBy(x => x.NormalizedName, StringComparer.Ordinal).ToArray();
        if (skills.Length == 0) return new(StringComparer.Ordinal);
        var ids = skills.Select(x => x.Id).ToArray();
        var labels = skills.Select(x => x.Name).ToArray();
        var normalized = skills.Select(x => x.NormalizedName).ToArray();
        var now = clock.GetUtcNow().UtcDateTime;
        // Parameterized arrays; deterministic conflict lock order for competing vocabulary writers.
        // READ COMMITTED's next statement sees a concurrent insert after ON CONFLICT has waited.
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO skills ("Id", "Name", "CreatedAtUtc", "UpdatedAtUtc")
            SELECT s.id, s.name, {now}, {now} FROM unnest({ids}::uuid[], {labels}::text[]) AS s(id, name)
            ORDER BY translate(btrim(s.name), 'ABCDEFGHIJKLMNOPQRSTUVWXYZ', 'abcdefghijklmnopqrstuvwxyz') COLLATE "C"
            ON CONFLICT ("NormalizedName") DO NOTHING
            """, ct);
        return await db.Skills.AsNoTracking().Where(x => normalized.Contains(x.NormalizedName)).ToDictionaryAsync(x => x.NormalizedName, StringComparer.Ordinal, ct);
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
        var vocabulary = await Canonical(skills.Select(x => x.Name), ct);
        foreach (var item in skills.OrderBy(x => new Skill(x.Name).NormalizedName, StringComparer.Ordinal))
        {
            var canonical = vocabulary[new Skill(item.Name).NormalizedName];
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
        var vocabulary = await Canonical(names, ct);
        foreach (var name in names.OrderBy(x => new Skill(x).NormalizedName, StringComparer.Ordinal))
        {
            var canonical = vocabulary[new Skill(name).NormalizedName];
            keep.Add(canonical.Id);
            if (existing.All(x => x.SkillId != canonical.Id)) owned.Add(new ResumeSkill(resume, canonical.Id));
        }
        foreach (var relation in existing.Where(x => !keep.Contains(x.SkillId))) owned.Remove(relation);
        await owned.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
    }
}
