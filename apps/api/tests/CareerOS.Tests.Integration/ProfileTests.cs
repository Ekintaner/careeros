using System.Net;
using System.Net.Http.Json;
using CareerOS.Application.Authentication;
using CareerOS.Application.Profiles;
using CareerOS.Domain.Accounts;
using CareerOS.Domain.Skills;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;
using Xunit;

namespace CareerOS.Tests.Integration;

[Collection("PostgreSQL")]
public sealed class ProfileTests(PostgreSqlFixture fixture)
{
    private async Task<TestDatabase> Database()
    {
        var database = await fixture.CreateDatabaseAsync(); await using var db = database.Context(); await db.Database.MigrateAsync(); return database;
    }
    private static async Task<AccountView> Register(Browser b, string email = "a@example.com") =>
        (await (await b.Send("register", new { email, password = "profile test secure passphrase", displayName = "Test person" })).Content.ReadFromJsonAsync<AccountView>())!;
    private static async Task<T> Read<T>(HttpResponseMessage response)
    { response.EnsureSuccessStatusCode(); return (await response.Content.ReadFromJsonAsync<T>())!; }
    private static ProfileInput Edit(ProfileView p) => new(" Backend engineer ", "Profile summary", "Riga", 4.5m, "Remote", ["Engineer", " engineer "], ["Riga", "Istanbul"], ["FullTime", "Contract"], ["Mid", "Senior"], p.Version);

    [PostgreSqlFact]
    public async Task EveryProductEndpointRequiresAuthentication()
    {
        await using var database = await Database(); using var host = new AuthFactory(database.ConnectionString); using var b = host.Browser();
        foreach (var path in new[] { "profile", "profile/skills", "skills?query=po", "account" }) Assert.Equal(HttpStatusCode.Unauthorized, (await b.Api(path)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await b.Api("profile", HttpMethod.Put, new { })).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await b.Api("account", HttpMethod.Put, new { })).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await b.Api("profile/skills", HttpMethod.Post, new { })).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await b.Api($"profile/skills/{Guid.NewGuid()}", HttpMethod.Put, new { })).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await b.Api($"profile/skills/{Guid.NewGuid()}?version=1", HttpMethod.Delete)).StatusCode);
    }
    [PostgreSqlFact]
    public async Task ProfilePersistsNormalizedPreferencesAndServerTimestampsAcrossContexts()
    {
        await using var database = await Database(); using var host = new AuthFactory(database.ConnectionString); using var b = host.Browser(); var account = await Register(b);
        var p = await Read<ProfileView>(await b.Api("profile")); Assert.Null(p.Headline); Assert.Empty(p.TargetRoles);
        host.Clock.Now = host.Clock.Now.AddSeconds(10);
        var saved = await Read<ProfileView>(await b.Api("profile", HttpMethod.Put, Edit(p)));
        Assert.Equal("Backend engineer", saved.Headline); Assert.Single(saved.TargetRoles); Assert.NotEqual(p.Version, saved.Version);
        await using var db = database.Context(); var persisted = await db.CareerProfiles.SingleAsync(x => x.UserId == account.Id);
        Assert.Equal(saved.TargetRoles, persisted.TargetRoles); Assert.Equal(host.Clock.Now.UtcDateTime.Ticks / 10, persisted.UpdatedAtUtc.Ticks / 10);
        Assert.Equal(p.UpdatedAtUtc, persisted.CreatedAtUtc); Assert.Equal(4.5m, persisted.YearsOfExperience);
        var reloaded = await Read<ProfileView>(await b.Api("profile"));
        Assert.Equal(saved.Version, reloaded.Version); Assert.Equal(saved.Headline, reloaded.Headline); Assert.Equal(saved.PreferredLocations, reloaded.PreferredLocations);
    }
    [PostgreSqlFact]
    public async Task ProfileAndAccountIgnoreForeignSelectorsAndRejectMassAssignment()
    {
        await using var database = await Database(); using var host = new AuthFactory(database.ConnectionString); using var a = host.Browser(); using var b = host.Browser();
        var accountA = await Register(a); var accountB = await Register(b, "b@example.com");
        var p = await Read<ProfileView>(await a.Api($"profile?userId={accountB.Id}"));
        Assert.Equal(HttpStatusCode.OK, (await a.Api($"profile?userId={accountB.Id}", HttpMethod.Put, Edit(p))).StatusCode);
        Assert.Null((await Read<ProfileView>(await b.Api("profile"))).Headline);
        Assert.Equal(HttpStatusCode.BadRequest, (await a.Api("profile", HttpMethod.Put, new { userId = accountB.Id, headline = "Injected", version = p.Version })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await a.Api("account", HttpMethod.Put, new { displayName = "Bad", email = "changed@example.com", version = 1 })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await a.Api("profile", HttpMethod.Put, new { createdAtUtc = "2000-01-01T00:00:00Z" })).StatusCode);
        await using var db = database.Context(); Assert.Equal("Backend engineer", (await db.CareerProfiles.SingleAsync(x => x.UserId == accountA.Id)).Headline);
        Assert.Equal("b@example.com", (await db.Users.SingleAsync(x => x.Id == accountB.Id)).Email);
    }
    [PostgreSqlFact]
    public async Task StaleAndSimultaneousProfileSavesConflictInsteadOfOverwriting()
    {
        await using var database = await Database(); using var host = new AuthFactory(database.ConnectionString); using var a = host.Browser(); await Register(a);
        var p = await Read<ProfileView>(await a.Api("profile"));
        var results = await Task.WhenAll(a.Api("profile", HttpMethod.Put, Edit(p)), a.Api("profile", HttpMethod.Put, Edit(p) with { Headline = "Other editor" }));
        Assert.Single(results, r => r.StatusCode == HttpStatusCode.OK); Assert.Single(results, r => r.StatusCode == HttpStatusCode.Conflict);
        Assert.Equal(HttpStatusCode.Conflict, (await a.Api("profile", HttpMethod.Put, Edit(p))).StatusCode);
    }
    [PostgreSqlFact]
    public async Task ProfileValidationAndCsrfFailWithoutSaving()
    {
        await using var database = await Database(); using var host = new AuthFactory(database.ConnectionString); using var a = host.Browser(); await Register(a);
        var p = await Read<ProfileView>(await a.Api("profile"));
        foreach (var invalid in new[] { Edit(p) with { YearsOfExperience = -1 }, Edit(p) with { Headline = new string('x', 201) }, Edit(p) with { TargetRoles = [""] }, Edit(p) with { SeniorityLevels = ["Junior"] }, Edit(p) with { PreferredLocations = null! }, Edit(p) with { PreferredWorkMode = "1" } })
            Assert.Equal(HttpStatusCode.BadRequest, (await a.Api("profile", HttpMethod.Put, invalid)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await a.Api("profile", HttpMethod.Put, Edit(p), csrf: false)).StatusCode);
        Assert.Null((await Read<ProfileView>(await a.Api("profile"))).Headline);
    }
    [PostgreSqlFact]
    public async Task MissingLegacyProfileReadsEmptyAndIsCreatedOnlyByExplicitSave()
    {
        await using var database = await Database(); using var host = new AuthFactory(database.ConnectionString); using var a = host.Browser(); await Register(a);
        await using (var db = database.Context()) { db.CareerProfiles.Remove(await db.CareerProfiles.SingleAsync()); await db.SaveChangesAsync(); }
        var p = await Read<ProfileView>(await a.Api("profile")); Assert.Equal(0u, p.Version);
        await using (var db = database.Context()) Assert.Empty(await db.CareerProfiles.ToListAsync());
        Assert.Equal(HttpStatusCode.OK, (await a.Api("profile", HttpMethod.Put, Edit(p))).StatusCode);
    }
    [PostgreSqlFact]
    public async Task SkillsReuseCanonicalNamesAndKeepMetadataOwned()
    {
        await using var database = await Database(); using var host = new AuthFactory(database.ConnectionString); using var a = host.Browser(); using var b = host.Browser(); await Register(a); await Register(b, "b@example.com");
        var skillA = await Read<SkillView>(await a.Api("profile/skills", HttpMethod.Post, new AddSkillInput(" PostgreSQL ", "Advanced", 3.5m)));
        var skillB = await Read<SkillView>(await b.Api("profile/skills", HttpMethod.Post, new AddSkillInput("POSTGRESQL", null, null)));
        Assert.Equal(skillA.SkillId, skillB.SkillId);
        Assert.Equal(HttpStatusCode.Conflict, (await a.Api("profile/skills", HttpMethod.Post, new AddSkillInput("postgresql", null, null))).StatusCode);
        var updated = await Read<SkillView>(await a.Api($"profile/skills/{skillA.SkillId}", HttpMethod.Put, new SkillInput("Expert", 4, skillA.Version)));
        Assert.Equal("Expert", updated.Proficiency);
        Assert.Null(Assert.Single(await Read<List<SkillView>>(await b.Api("profile/skills"))).Proficiency);
        Assert.Equal(HttpStatusCode.Conflict, (await a.Api($"profile/skills/{skillA.SkillId}?version={skillA.Version}", HttpMethod.Delete)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await a.Api($"profile/skills/{skillA.SkillId}?version={updated.Version}", HttpMethod.Delete)).StatusCode);
        Assert.Empty(await Read<List<SkillView>>(await a.Api("profile/skills"))); Assert.Single(await Read<List<SkillView>>(await b.Api("profile/skills")));
        await using var db = database.Context(); Assert.Single(await db.Skills.ToListAsync()); Assert.Single(await db.UserSkills.ToListAsync());
    }
    [PostgreSqlFact]
    public async Task ForeignSkillRelationshipsCannotBeUpdatedOrDeleted()
    {
        await using var database = await Database(); using var host = new AuthFactory(database.ConnectionString); using var a = host.Browser(); using var b = host.Browser(); await Register(a); await Register(b, "b@example.com");
        var skill = await Read<SkillView>(await b.Api("profile/skills", HttpMethod.Post, new AddSkillInput("C#", "Advanced", 4)));
        Assert.Equal(HttpStatusCode.NotFound, (await a.Api($"profile/skills/{skill.SkillId}", HttpMethod.Put, new SkillInput("Beginner", 0, skill.Version))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await a.Api($"profile/skills/{skill.SkillId}?version={skill.Version}", HttpMethod.Delete)).StatusCode);
        Assert.Equal("Advanced", Assert.Single(await Read<List<SkillView>>(await b.Api("profile/skills"))).Proficiency);
    }
    [PostgreSqlFact]
    public async Task ConcurrentCanonicalCreationProducesOneSkillAndTwoOwnedLinks()
    {
        await using var database = await Database(); using var host = new AuthFactory(database.ConnectionString); using var a = host.Browser(); using var b = host.Browser(); await Register(a); await Register(b, "b@example.com");
        var result = await Task.WhenAll(a.Api("profile/skills", HttpMethod.Post, new AddSkillInput("PostgreSQL", null, null)), b.Api("profile/skills", HttpMethod.Post, new AddSkillInput("POSTGRESQL", null, null)));
        Assert.All(result, r => Assert.Equal(HttpStatusCode.Created, r.StatusCode));
        await using var db = database.Context(); Assert.Single(await db.Skills.ToListAsync()); Assert.Equal(2, await db.UserSkills.CountAsync());
    }
    [PostgreSqlFact]
    public async Task SkillSearchIsAuthenticatedNormalizedBoundedAndLiteral()
    {
        await using var database = await Database(); using var host = new AuthFactory(database.ConnectionString); using var a = host.Browser(); await Register(a);
        await using (var db = database.Context()) { db.Skills.AddRange(Enumerable.Range(0, 25).Select(i => new Skill($"Post{i:D2}"))); db.Skills.Add(new Skill("Po%Literal")); await db.SaveChangesAsync(); }
        Assert.Equal(10, (await Read<List<SkillSuggestion>>(await a.Api("skills?query=%20POST%20"))).Count);
        Assert.Empty(await Read<List<SkillSuggestion>>(await a.Api("skills?query=p")));
        Assert.Single(await Read<List<SkillSuggestion>>(await a.Api("skills?query=po%25")));
        Assert.Equal(HttpStatusCode.BadRequest, (await a.Api("skills?query=" + new string('x', 101))).StatusCode);
    }
    [PostgreSqlFact]
    public async Task InvalidSkillMetadataDoesNotCreateCanonicalOrOwnedRows()
    {
        await using var database = await Database(); using var host = new AuthFactory(database.ConnectionString); using var a = host.Browser(); await Register(a);
        foreach (var input in new[] { new AddSkillInput("PostgreSQL", "Unknown", 2), new AddSkillInput("PostgreSQL", "Expert", 100.1m), new AddSkillInput("", null, null) })
            Assert.Equal(HttpStatusCode.BadRequest, (await a.Api("profile/skills", HttpMethod.Post, input)).StatusCode);
        await using var db = database.Context(); Assert.Empty(await db.Skills.ToListAsync()); Assert.Empty(await db.UserSkills.ToListAsync());
    }
    [PostgreSqlFact]
    public async Task DisplayNameEditingIsScopedValidatedAndDoesNotChangeCredentials()
    {
        await using var database = await Database(); using var host = new AuthFactory(database.ConnectionString); using var a = host.Browser(); using var b = host.Browser(); await Register(a); await Register(b, "b@example.com");
        var initial = await Read<AccountSettingsView>(await a.Api("account"));
        Assert.Equal(HttpStatusCode.BadRequest, (await a.Api("account", HttpMethod.Put, new AccountInput(" ", initial.Version))).StatusCode);
        var saved = await Read<AccountSettingsView>(await a.Api("account", HttpMethod.Put, new AccountInput(" Updated name ", initial.Version)));
        Assert.Equal("Updated name", saved.Account.DisplayName); Assert.Equal(initial.Account.Email, saved.Account.Email);
        Assert.Equal(HttpStatusCode.Conflict, (await a.Api("account", HttpMethod.Put, new AccountInput("Stale", initial.Version))).StatusCode);
        Assert.Equal("Test person", (await Read<AccountSettingsView>(await b.Api("account"))).Account.DisplayName);
        Assert.Equal("Updated name", (await Read<AccountView>(await a.Send("me", post: false))).DisplayName);
        Assert.Equal(HttpStatusCode.NoContent, (await a.Send("change-password", new { currentPassword = "profile test secure passphrase", newPassword = "changed profile secure passphrase" })).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await a.Api("profile")).StatusCode);
    }
    [PostgreSqlFact]
    public async Task PreferenceMigrationUpgradesExistingProfileRollsBackAndReapplies()
    {
        await using var database = await fixture.CreateDatabaseAsync(); await using var db = database.Context();
        await db.GetService<IMigrator>().MigrateAsync("AuthenticationFoundation");
        var id = Guid.NewGuid(); await database.ExecuteAsync($"INSERT INTO users (\"Id\",\"Email\",\"DisplayName\") VALUES ('{id}','legacy@example.com','Legacy'); INSERT INTO career_profiles (\"UserId\",\"Headline\") VALUES ('{id}','Existing career')");
        await db.Database.MigrateAsync(); var p = await db.CareerProfiles.SingleAsync(); Assert.Equal("Existing career", p.Headline); Assert.Empty(p.TargetRoles);
        Assert.True(await database.ScalarAsync<bool>("SELECT EXISTS (SELECT 1 FROM pg_indexes WHERE indexname='IX_skills_NormalizedName_prefix' AND indexdef LIKE '%varchar_pattern_ops%')"));
        var invalid = await Assert.ThrowsAsync<PostgresException>(() => database.ExecuteAsync($"UPDATE career_profiles SET \"EmploymentTypes\"=ARRAY['Invalid'] WHERE \"UserId\"='{id}'"));
        Assert.Equal(PostgresErrorCodes.CheckViolation, invalid.SqlState);
        await db.GetService<IMigrator>().MigrateAsync("AuthenticationFoundation");
        Assert.Equal("Existing career", await database.ScalarAsync<string>("SELECT \"Headline\" FROM career_profiles"));
        await db.Database.MigrateAsync(); Assert.False(db.Database.HasPendingModelChanges());
    }
}
