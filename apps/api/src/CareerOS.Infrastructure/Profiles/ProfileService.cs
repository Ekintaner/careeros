using CareerOS.Application.Authentication;
using CareerOS.Application.Profiles;
using CareerOS.Domain.Accounts;
using CareerOS.Domain.Skills;
using CareerOS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CareerOS.Infrastructure.Profiles;

public sealed class ProfileService(CareerOSDbContext db, OwnedData owned, ICurrentUser current, TimeProvider clock)
{
    private uint Version(object entity) => (uint)db.Entry(entity).Property("Version").CurrentValue!;
    private void Expect(object entity, uint version)
    {
        if (version == 0 || Version(entity) != version) throw new EditConflictException();
        db.Entry(entity).Property("Version").OriginalValue = version;
    }
    public async Task<ProfileView> Get(CancellationToken ct)
    {
        var profile = await owned.Query<CareerProfile>().SingleOrDefaultAsync(ct);
        return profile is null ? ProfileView.From(new CareerProfile(current.UserId), 0) : ProfileView.From(profile, Version(profile));
    }
    public async Task<ProfileView> Update(ProfileInput input, CancellationToken ct)
    {
        var profile = await owned.Query<CareerProfile>().SingleOrDefaultAsync(ct);
        if (profile is null)
        {
            if (input.Version != 0) throw new EditConflictException();
            profile = new CareerProfile(current.UserId);
            input.Apply(profile);
            owned.Add(profile);
        }
        else { Expect(profile, input.Version); input.Apply(profile); }
        await owned.SaveChangesAsync(ct);
        return ProfileView.From(profile, Version(profile));
    }
    public Task<List<SkillView>> Skills(CancellationToken ct) =>
        (from relation in owned.Query<UserSkill>()
         join skill in db.Skills on relation.SkillId equals skill.Id
         orderby skill.NormalizedName
         select new SkillView(skill.Id, skill.Name, relation.Proficiency == null ? null : relation.Proficiency.ToString(), relation.YearsExperience,
             EF.Property<uint>(relation, "Version"))).ToListAsync(ct);

    public async Task<SkillView> AddSkill(AddSkillInput input, CancellationToken ct)
    {
        var canonical = new Skill(input.Name);
        var relation = new UserSkill(current.UserId, canonical.Id);
        ProfileValidation.Assess(relation, input.Proficiency, input.YearsExperience);
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        // Shared vocabulary has a separate narrow policy: insert a validated name, never rename/delete another user's canonical entry.
        // ON CONFLICT waits for a competing insert; the next READ COMMITTED statement sees its committed row.
        var now = clock.GetUtcNow().UtcDateTime;
        await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO skills (\"Id\", \"Name\", \"CreatedAtUtc\", \"UpdatedAtUtc\") VALUES ({canonical.Id}, {canonical.Name}, {now}, {now}) ON CONFLICT (\"NormalizedName\") DO NOTHING", ct);
        var skill = await db.Skills.AsNoTracking().SingleAsync(x => x.NormalizedName == canonical.NormalizedName, ct);
        if (await owned.Query<UserSkill>().AnyAsync(x => x.SkillId == skill.Id, ct)) throw new EditConflictException();
        relation = new UserSkill(current.UserId, skill.Id);
        ProfileValidation.Assess(relation, input.Proficiency, input.YearsExperience);
        owned.Add(relation);
        await owned.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return new(skill.Id, skill.Name, relation.Proficiency?.ToString(), relation.YearsExperience, Version(relation));
    }
    public async Task<SkillView> UpdateSkill(Guid id, SkillInput input, CancellationToken ct)
    {
        var relation = await owned.Query<UserSkill>().SingleOrDefaultAsync(x => x.SkillId == id, ct) ?? throw new ResourceNotFoundException();
        Expect(relation, input.Version);
        ProfileValidation.Assess(relation, input.Proficiency, input.YearsExperience);
        await owned.SaveChangesAsync(ct);
        return new(id, await db.Skills.Where(x => x.Id == id).Select(x => x.Name).SingleAsync(ct), relation.Proficiency?.ToString(), relation.YearsExperience, Version(relation));
    }
    public async Task RemoveSkill(Guid id, uint version, CancellationToken ct)
    {
        var relation = await owned.Query<UserSkill>().SingleOrDefaultAsync(x => x.SkillId == id, ct) ?? throw new ResourceNotFoundException();
        Expect(relation, version);
        owned.Remove(relation);
        await owned.SaveChangesAsync(ct);
    }
    public async Task<List<SkillSuggestion>> Search(string? query, CancellationToken ct)
    {
        _ = current.UserId;
        if (string.IsNullOrWhiteSpace(query) || query.Trim().Length < 2) return [];
        var normalized = new Skill(query).NormalizedName;
        return await db.Skills.AsNoTracking().Where(x => x.NormalizedName.StartsWith(normalized)).OrderBy(x => x.NormalizedName)
            .Take(10).Select(x => new SkillSuggestion(x.Id, x.Name)).ToListAsync(ct);
    }
    public async Task<AccountSettingsView> Account(CancellationToken ct)
    {
        var user = await db.Users.SingleAsync(x => x.Id == current.UserId, ct);
        return new(AccountView.From(user), Version(user));
    }
    public async Task<AccountSettingsView> Rename(AccountInput input, CancellationToken ct)
    {
        // User is intentionally outside OwnedData; only this authenticated account and one allowed field may change.
        var user = await db.Users.SingleAsync(x => x.Id == current.UserId, ct);
        Expect(user, input.Version);
        user.Rename(input.DisplayName);
        await db.SaveChangesAsync(ct);
        return new(AccountView.From(user), Version(user));
    }
}
