using CareerOS.Application.Authentication;
using CareerOS.Infrastructure.Matching;
using CareerOS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using CareerOS.Application.Matching;
using CareerOS.Application.Opportunities;
using CareerOS.Infrastructure.Documents;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace CareerOS.Tests.Integration;
[Collection("PostgreSQL")]
public sealed class MatchingTests(PostgreSqlFixture fixture)
{
    private static async Task<T> Read<T>(HttpResponseMessage r) { r.EnsureSuccessStatusCode(); return (await r.Content.ReadFromJsonAsync<T>())!; }
    private static async Task Register(Browser b, string email) => (await b.Send("register", new { email, password = "matching tests passphrase", displayName = "Test" })).EnsureSuccessStatusCode();
    private static async Task<JobView> Job(Browser b)
    {
        var company = await Read<CompanyView>(await b.Api("companies", HttpMethod.Post, new CompanyInput("Company")));
        return await Read<JobView>(await b.Api("jobs", HttpMethod.Post, new JobInput(company.Id, "Developer")));
    }
    private static async Task<DocumentView> Resume(Browser b)
    {
        var content = new ByteArrayContent("%PDF-1.7\n1 0 obj <</Type /Catalog>> endobj\n%%EOF\n"u8.ToArray());
        content.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
        return await Read<DocumentView>(await b.Api("documents?name=CV&type=Resume&filename=cv.pdf", HttpMethod.Post, content));
    }
    private static async Task<MatchingView> Match(Browser b, Guid id, Guid? resume = null) => await Read<MatchingView>(await b.Api($"jobs/{id}/matching" + (resume.HasValue ? $"?resumeId={resume}" : "")));
    [PostgreSqlFact]
    public async Task ProfileAndResumeSourcesAreSeparateExactAndCurrent()
    {
        await using var d = await fixture.CreateDatabaseAsync(); await using (var db = d.Context()) await db.Database.MigrateAsync();
        using var h = new AuthFactory(d.ConnectionString); using var a = h.Browser(); await Register(a, "match@example.com");
        var job = await Job(a); var resume = await Resume(a);
        Assert.Null((await Match(a, job.Id)).Result.Score);
        (await a.Api("profile/skills", HttpMethod.Post, new { name = "C++" })).EnsureSuccessStatusCode();
        (await a.Api($"jobs/{job.Id}/skills", HttpMethod.Put, new JobSkillsInput(job.Version, [new("c++", "Required"), new("SQL", "Preferred"), new("C", "Preferred")]))).EnsureSuccessStatusCode();
        var profile = await Match(a, job.Id);
        Assert.Equal(60m, profile.Result.Score); Assert.Equal("Profile", profile.Source); Assert.Single(profile.Result.Skills, s => s.Matched);
        var emptyResume = await Match(a, job.Id, resume.ResumeId);
        Assert.Equal(0m, emptyResume.Result.Score); Assert.Equal("Resume", emptyResume.Source); Assert.Empty(emptyResume.Resume!.Skills);
        (await a.Api($"resumes/{resume.ResumeId}/skills", HttpMethod.Put, new ResumeSkillsInput(emptyResume.Resume.Version, ["sql"])) ).EnsureSuccessStatusCode();
        var evidenced = await Match(a, job.Id, resume.ResumeId);
        Assert.Equal(20m, evidenced.Result.Score); Assert.Equal("SQL", Assert.Single(evidenced.Result.Skills, s => s.Matched).Name);
        Assert.Equal(60m, (await Match(a, job.Id)).Result.Score);
        (await a.Api($"resumes/{resume.ResumeId}/skills", HttpMethod.Put, new ResumeSkillsInput(evidenced.Resume!.Version, []))).EnsureSuccessStatusCode();
        Assert.Equal(0m, (await Match(a, job.Id, resume.ResumeId)).Result.Score);
        var refreshed = await Match(a, job.Id);
        (await a.Api($"jobs/{job.Id}/skills", HttpMethod.Put, new JobSkillsInput(refreshed.JobVersion, []))).EnsureSuccessStatusCode();
        Assert.Null((await Match(a, job.Id)).Result.Score);
        // Matching never reads or changes the PDF bytes and removing requirements removes restrictive associations.
        Assert.Equal(HttpStatusCode.OK, (await a.Api($"documents/{resume.Id}/download")).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await a.Api($"jobs/{job.Id}?version={(await Match(a, job.Id)).JobVersion}", HttpMethod.Delete)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await a.Api($"documents/{resume.Id}", HttpMethod.Delete)).StatusCode);
    }
    [PostgreSqlFact]
    public async Task TwoUsersCannotReadOrWriteEachOthersRequirementsOrEvidence()
    {
        await using var d = await fixture.CreateDatabaseAsync(); await using (var db = d.Context()) await db.Database.MigrateAsync();
        using var h = new AuthFactory(d.ConnectionString); using var a = h.Browser(); using var b = h.Browser(); using var anon = h.Browser();
        await Register(a, "match-a@example.com"); await Register(b, "match-b@example.com");
        var ja = await Job(a); var jb = await Job(b); var ra = await Resume(a); var rb = await Resume(b);
        var ma = await Match(a, ja.Id, ra.ResumeId);
        foreach (var path in new[] { $"jobs/{ja.Id}/matching", $"resumes/{ra.ResumeId}/skills" })
        {
            Assert.Equal(HttpStatusCode.Unauthorized, (await anon.Api(path)).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await b.Api(path)).StatusCode);
        }
        Assert.Equal(HttpStatusCode.NotFound, (await a.Api($"jobs/{ja.Id}/matching?resumeId={rb.ResumeId}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await b.Api($"jobs/{ja.Id}/skills", HttpMethod.Put, new JobSkillsInput(ja.Version, [new("Private", "Required")]))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await b.Api($"resumes/{ra.ResumeId}/skills", HttpMethod.Put, new ResumeSkillsInput(ma.Resume!.Version, ["Private"])) ).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await a.Api($"jobs/{ja.Id}/skills", HttpMethod.Put, new JobSkillsInput(ja.Version, []), csrf: false)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anon.Api($"jobs/{ja.Id}/skills", HttpMethod.Put, new JobSkillsInput(ja.Version, []))).StatusCode);
        (await a.Api($"jobs/{ja.Id}/skills", HttpMethod.Put, new JobSkillsInput(ja.Version, [new("SQL", "Required")]))).EnsureSuccessStatusCode();
        (await b.Api("profile/skills", HttpMethod.Post, new { name = "SQL" })).EnsureSuccessStatusCode();
        Assert.Equal(0m, (await Match(a, ja.Id)).Result.Score);
        Assert.Null((await Match(b, jb.Id)).Result.Score);
        var response = await a.Api($"jobs/{ja.Id}/matching"); Assert.Equal("no-store", response.Headers.CacheControl?.ToString());
        var json = await response.Content.ReadAsStringAsync(); Assert.DoesNotContain("userId", json, StringComparison.OrdinalIgnoreCase); Assert.DoesNotContain("storageKey", json, StringComparison.OrdinalIgnoreCase);
    }
    [PostgreSqlFact]
    public async Task ValidationConcurrencyAndTransactionRollbackProtectWholeSets()
    {
        await using var d = await fixture.CreateDatabaseAsync(); await using (var db = d.Context()) await db.Database.MigrateAsync();
        using var h = new AuthFactory(d.ConnectionString); using var a = h.Browser(); await Register(a, "match-conflict@example.com");
        var j = await Job(a); var r = await Resume(a); var initial = await Match(a, j.Id, r.ResumeId);
        foreach (var body in new object[] { new { version = j.Version, skills = new[] { new { name = "SQL", requirementLevel = "1" } } }, new { version = j.Version, skills = new[] { new { name = "SQL", requirementLevel = "Required" } }, userId = Guid.NewGuid() }, new { version = j.Version, skills = (object?)null } })
            Assert.Equal(HttpStatusCode.BadRequest, (await a.Api($"jobs/{j.Id}/skills", HttpMethod.Put, body)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await a.Api($"jobs/{j.Id}/matching?resumeId=invalid")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await a.Api($"resumes/{r.ResumeId}/skills", HttpMethod.Put, new ResumeSkillsInput(initial.Resume!.Version, ["SQL", "sql"])) ).StatusCode);
        var writes = await Task.WhenAll(a.Api($"jobs/{j.Id}/skills", HttpMethod.Put, new JobSkillsInput(j.Version, [new("SQL", "Required")])), a.Api($"jobs/{j.Id}/skills", HttpMethod.Put, new JobSkillsInput(j.Version, [new("C#", "Required")])));
        Assert.Single(writes, x => x.StatusCode == HttpStatusCode.NoContent); Assert.Single(writes, x => x.StatusCode == HttpStatusCode.Conflict);
        var latest = await Match(a, j.Id, r.ResumeId); Assert.Single(latest.Result.Skills);
        var resumeWrites = await Task.WhenAll(a.Api($"resumes/{r.ResumeId}/skills", HttpMethod.Put, new ResumeSkillsInput(latest.Resume!.Version, ["SQL"])), a.Api($"resumes/{r.ResumeId}/skills", HttpMethod.Put, new ResumeSkillsInput(latest.Resume.Version, ["C#"])));
        Assert.Single(resumeWrites, x => x.StatusCode == HttpStatusCode.NoContent); Assert.Single(resumeWrites, x => x.StatusCode == HttpStatusCode.Conflict);
        await d.ExecuteAsync("CREATE FUNCTION reject_skill() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN RAISE EXCEPTION 'simulated failure'; END; $$; CREATE TRIGGER reject_skill BEFORE INSERT ON job_skills FOR EACH ROW EXECUTE FUNCTION reject_skill();");
        Assert.Equal(HttpStatusCode.InternalServerError, (await a.Api($"jobs/{j.Id}/skills", HttpMethod.Put, new JobSkillsInput(latest.JobVersion, [new("Never committed skill", "Preferred")]))).StatusCode);
        var unchanged = await Match(a, j.Id); Assert.Equal(latest.JobVersion, unchanged.JobVersion); Assert.Equal(latest.Result.Skills, unchanged.Result.Skills);
        await using var dbAfter = d.Context(); Assert.False(await dbAfter.Skills.AnyAsync(s => s.Name == "Never committed skill"));
    }
    [PostgreSqlFact]
    public async Task MatchQueriesAreConstantAndProjectedForOneHundredRequirements()
    {
        await using var d = await fixture.CreateDatabaseAsync(); await using (var original = d.Context()) await original.Database.MigrateAsync();
        using var h = new AuthFactory(d.ConnectionString); using var a = h.Browser(); await Register(a, "match-queries@example.com");
        var job = await Job(a); var resume = await Resume(a);
        (await a.Api($"jobs/{job.Id}/skills", HttpMethod.Put, new JobSkillsInput(job.Version, Enumerable.Range(0, 100).Select(x => new RequirementInput("Skill " + x, "Required")).ToArray()))).EnsureSuccessStatusCode();
        var commands = new List<string>();
        Guid owner; await using (var original = d.Context()) owner = await original.Users.Select(x => x.Id).SingleAsync();
        await using var db = new CareerOSDbContext(new DbContextOptionsBuilder<CareerOSDbContext>().UseNpgsql(d.ConnectionString).LogTo(commands.Add, [RelationalEventId.CommandExecuted]).Options);
        var service = new MatchingService(db, new OwnedData(db, new Current(owner)), TimeProvider.System, new Current(owner));
        var profile = await service.Get(job.Id, null, default);
        Assert.Equal(100, profile.Result.Skills.Length); Assert.Equal(3, commands.Count);
        Assert.DoesNotContain(commands, x => x.Contains("FROM skills AS") && !x.Contains("JOIN"));
        Assert.DoesNotContain(commands, x => x.Contains("StorageKey") || x.Contains("FROM documents") || x.Contains("Description"));
        commands.Clear(); var cv = await service.Get(job.Id, resume.ResumeId, default);
        Assert.Equal(100, cv.Result.Skills.Length); Assert.Equal(4, commands.Count);
        Assert.DoesNotContain(commands, x => x.Contains("FROM user_skills"));
        // The same snapshot can be saved once; even a no-op replacement advances the parent's xmin.
        var view = await Match(a, job.Id, resume.ResumeId);
        (await a.Api($"resumes/{resume.ResumeId}/skills", HttpMethod.Put, new ResumeSkillsInput(view.Resume!.Version, []))).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.Conflict, (await a.Api($"resumes/{resume.ResumeId}/skills", HttpMethod.Put, new ResumeSkillsInput(view.Resume.Version, []))).StatusCode);
    }
    [PostgreSqlFact]
    public async Task ResumeEvidenceEditsAndDocumentDeletionShareSafeLifecycleOrdering()
    {
        await using var d = await fixture.CreateDatabaseAsync(); await using (var db = d.Context()) await db.Database.MigrateAsync();
        using var h = new AuthFactory(d.ConnectionString); using var a = h.Browser(); await Register(a, "match-delete@example.com");
        var job = await Job(a);
        for (var attempt = 0; attempt < 3; attempt++)
        {
            var document = await Resume(a); var original = await Match(a, job.Id, document.ResumeId);
            (await a.Api($"resumes/{document.ResumeId}/skills", HttpMethod.Put, new ResumeSkillsInput(original.Resume!.Version, ["SQL"])) ).EnsureSuccessStatusCode();
            var latest = await Match(a, job.Id, document.ResumeId);
            var responses = await Task.WhenAll(a.Api($"resumes/{document.ResumeId}/skills", HttpMethod.Put, new ResumeSkillsInput(latest.Resume!.Version, ["C#"])), a.Api($"documents/{document.Id}", HttpMethod.Delete));
            Assert.Contains(responses[0].StatusCode, new[] { HttpStatusCode.NoContent, HttpStatusCode.NotFound });
            Assert.Equal(HttpStatusCode.NoContent, responses[1].StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await a.Api($"resumes/{document.ResumeId}/skills")).StatusCode);
        }
        await using var final = d.Context(); Assert.Empty(await final.ResumeSkills.ToArrayAsync()); Assert.Empty(await final.Resumes.ToArrayAsync());
    }    private sealed record Current(Guid UserId) : ICurrentUser;}
