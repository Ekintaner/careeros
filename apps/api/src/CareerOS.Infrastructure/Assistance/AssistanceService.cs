using System.Data;
using System.Globalization;
using System.Text;
using CareerOS.Application.Assistance;
using CareerOS.Application.Applications;
using CareerOS.Domain.Accounts;
using CareerOS.Domain.Documents;
using CareerOS.Domain.Opportunities;
using CareerOS.Domain.Skills;
using CareerOS.Domain.Common;
using CareerOS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
namespace CareerOS.Infrastructure.Assistance;
public sealed class AssistanceService(CareerOSDbContext db, OwnedData owned, ILocalAiProvider provider, TimeProvider clock)
{
    public async Task<AssistanceView> Generate(Guid jobId, AssistanceInput input, CancellationToken ct)
    {
        input.Validate(); AssistanceContext context;
        await using (var tx = await db.Database.BeginTransactionAsync(IsolationLevel.RepeatableRead, ct))
        {
            var job = await owned.Query<Job>().AsNoTracking().Where(x => x.Id == jobId).Select(x => new { x.Title, x.Description }).SingleOrDefaultAsync(ct) ?? throw new ApplicationNotFoundException();
            var facts = new List<RecordedFact>();
            if (input.ResumeId.HasValue)
            {
                if (!await owned.Query<Resume>().AnyAsync(x => x.Id == input.ResumeId, ct)) throw new ApplicationNotFoundException();
                var profile = await owned.Query<CareerProfile>().AsNoTracking().SingleOrDefaultAsync(ct);
                if (profile?.Headline is { } headline) facts.Add(new("profile:headline", "profile headline", headline));
                if (profile?.Summary is { } summary) facts.Add(new("profile:summary", "profile summary", summary));
                if (profile?.YearsOfExperience is { } years) facts.Add(new("profile:experience", "profile years of experience", years.ToString(CultureInfo.InvariantCulture)));
                var ps = await (from r in owned.Query<UserSkill>() join s in db.Skills on r.SkillId equals s.Id orderby s.NormalizedName select new RecordedFact("profile-skill:" + s.Id, "profile skill", s.Name)).Take(101).ToArrayAsync(ct);
                var rs = await (from r in owned.Query<ResumeSkill>() where r.ResumeId == input.ResumeId join s in db.Skills on r.SkillId equals s.Id orderby s.NormalizedName select new RecordedFact("resume-skill:" + s.Id, "selected resume skill", s.Name)).Take(101).ToArrayAsync(ct);
                if (ps.Length > 100 || rs.Length > 100) throw new DomainValidationException("Local assistance supports at most 100 profile and 100 selected resume skills.");
                facts.AddRange(ps); facts.AddRange(rs);
            }
            if (string.IsNullOrWhiteSpace(job.Description)) throw new DomainValidationException("Add a saved job description before requesting assistance.");
            if (job.Description.Length > AssistanceRules.DescriptionLimit) throw new DomainValidationException("Use a job description of at most 10,000 characters for local assistance; no content is silently truncated.");
            if (input.Mode == "resume" && facts.Count == 0) throw new DomainValidationException("Record career-profile information or explicit resume skills first. AI cannot invent evidence.");
            context = new(input.Mode, job.Title, job.Description, facts.ToArray());
            if (Encoding.UTF8.GetByteCount(AssistanceRules.Serialize(context)) > 24000) throw new DomainValidationException("The combined saved context is too large for local assistance. Shorten the description or profile summary.");
            await tx.CommitAsync(ct);
        }
        // No database transaction is held while inference runs. The response describes this request's snapshot and never writes facts.
        var output = await provider.Generate(context, ct);
        return AssistanceRules.Validate(output, context, provider.Model, clock.GetUtcNow().UtcDateTime);
    }
}
