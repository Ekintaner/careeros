using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using CareerOS.Application.Authentication;
using CareerOS.Application.Intelligence;
using CareerOS.Application.Matching;
using CareerOS.Application.Opportunities;
using CareerOS.Infrastructure.Documents;
using CareerOS.Infrastructure.Intelligence;
using CareerOS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Xunit;

namespace CareerOS.Tests.Integration;
[Collection("PostgreSQL")]
public sealed class IntelligenceTests(PostgreSqlFixture fixture)
{
    private static async Task<T> Read<T>(HttpResponseMessage r) { r.EnsureSuccessStatusCode(); return (await r.Content.ReadFromJsonAsync<T>())!; }
    private static async Task Register(Browser b, string email) => (await b.Send("register", new { email, password = "intelligence tests passphrase", displayName = "Test" })).EnsureSuccessStatusCode();
    private static async Task<JobView> Job(Browser b, string title, params RequirementInput[] skills)
    {
        var c = await Read<CompanyView>(await b.Api("companies", HttpMethod.Post, new CompanyInput("Company")));
        var j = await Read<JobView>(await b.Api("jobs", HttpMethod.Post, new JobInput(c.Id, title)));
        if (skills.Length > 0) (await b.Api($"jobs/{j.Id}/skills", HttpMethod.Put, new JobSkillsInput(j.Version, skills))).EnsureSuccessStatusCode();
        return await Read<JobView>(await b.Api($"jobs/{j.Id}"));
    }
    private static async Task<Guid> Resume(Browser b, params string[] names)
    {
        var content = new ByteArrayContent("%PDF-1.7\n1 0 obj <</Type /Catalog>> endobj\n%%EOF\n"u8.ToArray()); content.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
        var document = await Read<DocumentView>(await b.Api("documents?name=CV&type=Resume&filename=cv.pdf", HttpMethod.Post, content));
        var r = await Read<ResumeSkillsView>(await b.Api($"resumes/{document.ResumeId}/skills"));
        (await b.Api($"resumes/{r.Id}/skills", HttpMethod.Put, new ResumeSkillsInput(r.Version, names))).EnsureSuccessStatusCode();
        return r.Id;
    }
    private static async Task<(JobView One, JobView Two, JobView Archived)> Seed(Browser a)
    {
        (await a.Api("profile/skills", HttpMethod.Post, new { name = "SQL" })).EnsureSuccessStatusCode();
        (await a.Api("profile/skills", HttpMethod.Post, new { name = "React" })).EnsureSuccessStatusCode();
        await Resume(a, "sql", "Go");
        var one = await Job(a, "One", new("SQL", "Required"), new("Docker", "Required"), new("React", "Preferred"), new("Go", "Preferred"));
        var two = await Job(a, "Two", new("docker", "Required"), new("SQL", "Preferred"));
        await Job(a, "Unassessed"); var archived = await Job(a, "Archived", new RequirementInput("Rust", "Required"));
        archived = await Read<JobView>(await a.Api($"jobs/{archived.Id}/archive", HttpMethod.Put, new ArchiveInput(true, archived.Version)));
        return (one, two, archived);
    }
    [PostgreSqlFact]
    public async Task RealDemandCoveragePrioritiesAndJobDrilldownReuseMatchingRules()
    {
        await using var d = await fixture.CreateDatabaseAsync(); await using (var db = d.Context()) await db.Database.MigrateAsync();
        using var h = new AuthFactory(d.ConnectionString); using var a = h.Browser(); await Register(a, "intelligence@example.com"); var jobs = await Seed(a);
        var view = await Read<IntelligenceView>(await a.Api("intelligence/skills")); var s = view.Summary;
        Assert.Equal(3, s.TotalJobs); Assert.Equal(2, s.AnalyzedJobs); Assert.Equal(1, s.UnassessedJobs); Assert.Equal(4, s.DistinctSkills);
        Assert.Equal(1, s.SupportedSkills); Assert.Equal(2, s.EvidenceGapSkills); Assert.Equal(1, s.MissingSkills); Assert.Equal(2, s.ProfileSkills);
        Assert.Equal(5, s.EarnedProfilePoints); Assert.Equal(12, s.PossiblePoints); Assert.Equal(41.7m, s.ProfileCoverage); Assert.False(view.HistoryAvailable);
        var docker = Assert.Single(view.Items, x => x.Name == "Docker"); Assert.Equal(docker, view.Items[0]);
        Assert.Equal(2, docker.RequiredJobs); Assert.Equal(100m, docker.DemandPercent); Assert.Equal(100m, docker.LearningPriority);
        Assert.Equal("missing", docker.Status); Assert.Equal("Learn or assess", docker.Action);
        var sql = Assert.Single(view.Items, x => x.Name == "SQL"); Assert.Equal("supported", sql.Status); Assert.Equal(0m, sql.ActionPriority);
        var react = Assert.Single(view.Items, x => x.Name == "React"); Assert.True(react.InProfile); Assert.Equal(0, react.ConfirmedResumes); Assert.Equal("Document resume evidence", react.Action); Assert.Equal(0m, react.LearningPriority);
        var go = Assert.Single(view.Items, x => x.Name == "Go"); Assert.False(go.InProfile); Assert.Equal(1, go.ConfirmedResumes); Assert.Equal("Review profile record", go.Action); Assert.Equal(0m, go.LearningPriority);
        var drilldown = await Read<SkillJobsView>(await a.Api($"intelligence/skills/{docker.SkillId}/jobs")); Assert.Equal(2, drilldown.TotalCount);
        foreach (var job in drilldown.Items)
        {
            var match = await Read<MatchingView>(await a.Api($"jobs/{job.Id}/matching")); Assert.Equal(match.Result.Score, job.ProfileCoverage);
            Assert.Equal("Required", job.RequirementLevel); Assert.Equal("Company", job.CompanyName);
        }
        var archived = await Read<IntelligenceView>(await a.Api("intelligence/skills?state=archived")); Assert.Equal(1, archived.Summary.TotalJobs); Assert.Equal("Rust", Assert.Single(archived.Items).Name);
        var all = await Read<IntelligenceView>(await a.Api("intelligence/skills?state=all")); Assert.Equal(4, all.Summary.TotalJobs); Assert.Equal(33.3m, all.Summary.ProfileCoverage);
        Assert.Equal(66.7m, Assert.Single(all.Items, x => x.Name == "Docker").LearningPriority);
        var missing = await Read<IntelligenceView>(await a.Api("intelligence/skills?status=missing&pageSize=1")); Assert.Equal(1, missing.TotalCount); Assert.Single(missing.Items); Assert.Equal(view.Summary, missing.Summary);
        var search = await Read<IntelligenceView>(await a.Api("intelligence/skills?search=SQL&sort=name")); Assert.Equal(sql, Assert.Single(search.Items));
        Assert.Empty((await Read<IntelligenceView>(await a.Api("intelligence/skills?page=10000"))).Items);
    }
    [PostgreSqlFact]
    public async Task TwoUserIsolationAnonymousAccessAndQueryValidation()
    {
        await using var d = await fixture.CreateDatabaseAsync(); await using (var db = d.Context()) await db.Database.MigrateAsync();
        using var h = new AuthFactory(d.ConnectionString); using var a = h.Browser(); using var b = h.Browser(); using var anon = h.Browser();
        await Register(a, "intel-a@example.com"); await Register(b, "intel-b@example.com"); await Seed(a);
        await Job(b, "B private", new("PrivateB", "Required"), new("SQL", "Required"));
        (await b.Api("profile/skills", HttpMethod.Post, new { name = "Docker" })).EnsureSuccessStatusCode(); await Resume(b, "Docker", "SQL");
        var va = await Read<IntelligenceView>(await a.Api("intelligence/skills?state=all")); var vb = await Read<IntelligenceView>(await b.Api("intelligence/skills?state=all"));
        Assert.Equal(4, va.Summary.TotalJobs); Assert.Equal(1, vb.Summary.TotalJobs);
        Assert.DoesNotContain(va.Items, x => x.Name == "PrivateB"); Assert.Equal(0, Assert.Single(va.Items, x => x.Name == "Docker").ConfirmedResumes);
        Assert.False(Assert.Single(va.Items, x => x.Name == "Docker").InProfile);
        var privateSkill = Assert.Single(vb.Items, x => x.Name == "PrivateB");
        Assert.Equal(HttpStatusCode.NotFound, (await a.Api($"intelligence/skills/{privateSkill.SkillId}/jobs")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anon.Api("intelligence/skills")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anon.Api($"intelligence/skills/{privateSkill.SkillId}/jobs")).StatusCode);
        var sqlA = Assert.Single(va.Items, x => x.Name == "SQL");
        var sqlJobs = await Read<SkillJobsView>(await a.Api($"intelligence/skills/{sqlA.SkillId}/jobs?state=all")); Assert.Equal(2, sqlJobs.TotalCount); Assert.DoesNotContain(sqlJobs.Items, x => x.Title == "B private");
        foreach (var suffix in new[] { "page=0", "pageSize=51", "state=unknown", "status=unknown", "sort=sql", "search=" + new string('a', 101), "page=not-a-number" })
        {
            Assert.Equal(HttpStatusCode.BadRequest, (await a.Api("intelligence/skills?" + suffix)).StatusCode);
            Assert.Equal(HttpStatusCode.BadRequest, (await a.Api($"intelligence/skills/{sqlA.SkillId}/jobs?" + suffix)).StatusCode);
        }
        var response = await a.Api("intelligence/skills"); Assert.Equal("no-store", response.Headers.CacheControl?.ToString());
        var json = await response.Content.ReadAsStringAsync(); foreach (var secret in new[] { "userId", "password", "storageKey", "B private" }) Assert.DoesNotContain(secret, json, StringComparison.OrdinalIgnoreCase);
    }
    [PostgreSqlFact]
    public async Task EmptyAndUnassessedDataStayUnknownAndEditsRecomputeWithoutHistory()
    {
        await using var d = await fixture.CreateDatabaseAsync(); await using (var db = d.Context()) await db.Database.MigrateAsync();
        using var h = new AuthFactory(d.ConnectionString); using var a = h.Browser(); await Register(a, "intel-empty@example.com");
        var empty = await Read<IntelligenceView>(await a.Api("intelligence/skills")); Assert.Empty(empty.Items); Assert.Null(empty.Summary.ProfileCoverage); Assert.Equal(0, empty.Summary.TotalJobs);
        await Job(a, "No requirements"); var unassessed = await Read<IntelligenceView>(await a.Api("intelligence/skills")); Assert.Equal(1, unassessed.Summary.UnassessedJobs); Assert.Null(unassessed.Summary.ProfileCoverage);
        var jobs = await Seed(a); var initial = await Read<IntelligenceView>(await a.Api("intelligence/skills"));
        (await a.Api("profile/skills", HttpMethod.Post, new { name = "Docker" })).EnsureSuccessStatusCode();
        var changed = await Read<IntelligenceView>(await a.Api("intelligence/skills")); var docker = Assert.Single(changed.Items, x => x.Name == "Docker");
        Assert.Equal(91.7m, changed.Summary.ProfileCoverage); Assert.Equal("evidenceGap", docker.Status); Assert.Equal(0m, docker.LearningPriority);
        await Resume(a, "Docker"); var supported = await Read<IntelligenceView>(await a.Api("intelligence/skills")); Assert.Equal("supported", Assert.Single(supported.Items, x => x.Name == "Docker").Status);
        Assert.False(supported.HistoryAvailable); Assert.Equal(initial.Summary.AnalyzedJobs, supported.Summary.AnalyzedJobs);
        var j = await Read<JobView>(await a.Api($"jobs/{jobs.One.Id}")); (await a.Api($"jobs/{j.Id}/skills", HttpMethod.Put, new JobSkillsInput(j.Version, []))).EnsureSuccessStatusCode();
        var removed = await Read<IntelligenceView>(await a.Api("intelligence/skills")); Assert.Equal(1, removed.Summary.AnalyzedJobs); Assert.DoesNotContain(removed.Items, x => x.Name == "React");
    }
    [PostgreSqlFact]
    public async Task QueriesAreBoundedProjectedAndDoNotGrowWithSkillCount()
    {
        await using var d = await fixture.CreateDatabaseAsync(); await using (var db = d.Context()) await db.Database.MigrateAsync();
        using var h = new AuthFactory(d.ConnectionString); using var a = h.Browser(); await Register(a, "intel-queries@example.com");
        await Job(a, "Large", Enumerable.Range(0, 100).Select(x => new RequirementInput("Skill " + x, "Required")).ToArray());
        Guid owner; await using (var db = d.Context()) owner = await db.Users.Select(x => x.Id).SingleAsync();
        var commands = new List<string>(); await using var context = new CareerOSDbContext(new DbContextOptionsBuilder<CareerOSDbContext>().UseNpgsql(d.ConnectionString).LogTo(commands.Add, [RelationalEventId.CommandExecuted]).Options);
        var service = new IntelligenceService(context, new OwnedData(context, new Current(owner)));
        var result = await service.Get(new(PageSize: 2), default); Assert.Equal(100, result.TotalCount); Assert.Equal(2, result.Items.Count); Assert.Equal(4, commands.Count);
        Assert.Contains(commands, x => x.Contains("GROUP BY")); Assert.Contains(commands, x => x.Contains("LIMIT") && x.Contains("OFFSET"));
        Assert.True(!commands.Any(x => x.Contains("Description") || x.Contains("StorageKey") || x.Contains("PasswordHash")), string.Join("\n", commands));
        commands.Clear(); var jobs = await service.SkillJobs(result.Items[0].SkillId, new(PageSize: 1), default); Assert.Single(jobs.Items); Assert.Equal(3, commands.Count); Assert.Contains(commands, x => x.Contains("LIMIT") && x.Contains("OFFSET"));
    }
    private sealed record Current(Guid UserId) : ICurrentUser;
}
