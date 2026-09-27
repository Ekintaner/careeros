using System.Net;
using System.Net.Http.Json;
using CareerOS.Application.Applications;
using CareerOS.Application.Authentication;
using CareerOS.Application.Opportunities;
using CareerOS.Domain.Applications;
using CareerOS.Domain.Documents;
using CareerOS.Infrastructure.Applications;
using CareerOS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Xunit;

namespace CareerOS.Tests.Integration;
[Collection("PostgreSQL")]
public sealed class ApplicationTests(PostgreSqlFixture fixture)
{
    private async Task<TestDatabase> Database() { var d = await fixture.CreateDatabaseAsync(); await using var db = d.Context(); await db.Database.MigrateAsync(); return d; }
    private static async Task<T> Read<T>(HttpResponseMessage r) { r.EnsureSuccessStatusCode(); return (await r.Content.ReadFromJsonAsync<T>())!; }
    private static async Task Register(Browser b, string email = "applications@example.com") => (await b.Send("register", new { email, password = "application test passphrase", displayName = "Test person" })).EnsureSuccessStatusCode();
    private static async Task<JobView> Job(Browser b, string title = "Engineer", string company = "Example")
    { var c = await Read<CompanyView>(await b.Api("companies", HttpMethod.Post, new CompanyInput(company))); return await Read<JobView>(await b.Api("jobs", HttpMethod.Post, new JobInput(c.Id, title, Description: "Not for cards", Location: "Riga", SourceUrl: "https://example.com/job"))); }
    private static async Task<ApplicationView> Create(Browser b, Guid job, string status = "Saved") => await Read<ApplicationView>(await b.Api("applications", HttpMethod.Post, new CreateApplicationInput(job, status)));
    private static async Task<ApplicationView> Move(Browser b, ApplicationView a, string status) => await Read<ApplicationView>(await b.Api($"applications/{a.Id}/status", HttpMethod.Post, new StatusInput(status, a.Version)));
    [PostgreSqlFact]
    public async Task CreationInitialHistoryDuplicateDetailsAndMetadataPersist()
    {
        await using var d = await Database(); using var h = new AuthFactory(d.ConnectionString); using var b = h.Browser(); await Register(b);
        var job = await Job(b); var a = await Create(b, job.Id); Assert.Equal("Saved", a.Status); Assert.Null(a.AppliedAtUtc);
        var duplicate = await b.Api("applications", HttpMethod.Post, new CreateApplicationInput(job.Id)); Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode); Assert.Contains(a.Id.ToString(), await duplicate.Content.ReadAsStringAsync());
        var history = await Read<Page<HistoryView>>(await b.Api($"applications/{a.Id}/history")); var first = Assert.Single(history.Items); Assert.Null(first.FromStatus); Assert.Equal("Saved", first.ToStatus);
        var applied = await Create(b, (await Job(b, "Analyst")).Id, "Applied"); var initial = Assert.Single((await Read<Page<HistoryView>>(await b.Api($"applications/{applied.Id}/history"))).Items);
        Assert.Null(initial.FromStatus); Assert.Equal("Applied", initial.ToStatus); Assert.NotNull(applied.AppliedAtUtc);
        h.Clock.Now = h.Clock.Now.AddSeconds(2);
        var date = applied.AppliedAtUtc!.Value.AddDays(-2);
        var edited = await Read<ApplicationView>(await b.Api($"applications/{applied.Id}", HttpMethod.Put, new EditApplicationInput(applied.Version, Source: " Referral ", AppliedAtUtc: date)));
        Assert.Equal("Referral", edited.Source); Assert.Equal(date.Ticks / 10, edited.AppliedAtUtc!.Value.Ticks / 10); Assert.Equal("Example", edited.CompanyName); Assert.Equal("https://example.com/job", edited.SourceUrl);
        await using var reopened = d.Context(); Assert.Equal(2, await reopened.Applications.CountAsync()); Assert.Equal(2, await reopened.ApplicationStatusHistory.CountAsync());
        Assert.Equal(HttpStatusCode.Conflict, (await b.Api($"applications/{a.Id}", HttpMethod.Delete)).StatusCode);
        Assert.Equal(2, await reopened.Applications.CountAsync());
    }
    [PostgreSqlFact]
    public async Task TransitionsAreAtomicOrderedConcurrentAndPreserveAppliedDate()
    {
        await using var d = await Database(); using var h = new AuthFactory(d.ConnectionString); using var b = h.Browser(); await Register(b);
        var a = await Create(b, (await Job(b)).Id); var original = a;
        Assert.Equal(HttpStatusCode.BadRequest, (await b.Api($"applications/{a.Id}/status", HttpMethod.Post, new StatusInput("Accepted", a.Version))).StatusCode);
        a = await Move(b, a, "Applied"); var applied = a.AppliedAtUtc;
        Assert.Equal(HttpStatusCode.Conflict, (await b.Api($"applications/{a.Id}/status", HttpMethod.Post, new StatusInput("Interview", original.Version))).StatusCode);
        foreach (var stage in new[] { "Interview", "Offer", "Assessment", "Applied", "Offer", "Accepted" }) { h.Clock.Now = h.Clock.Now.AddMinutes(1); a = await Move(b, a, stage); }
        Assert.Equal(applied, a.AppliedAtUtc); Assert.Equal(HttpStatusCode.BadRequest, (await b.Api($"applications/{a.Id}/status", HttpMethod.Post, new StatusInput("Applied", a.Version))).StatusCode);
        var rows = (await Read<Page<HistoryView>>(await b.Api($"applications/{a.Id}/history"))).Items;
        Assert.Equal(8, rows.Count); Assert.Equal(Enumerable.Range(0, 8), rows.Select(x => x.Sequence)); Assert.Equal("Accepted", rows.Last().ToStatus);
        var archived = await Read<ApplicationView>(await b.Api($"applications/{a.Id}/archive", HttpMethod.Post, new VersionInput(a.Version)));
        Assert.Empty((await Read<Page<ApplicationCard>>(await b.Api("applications"))).Items);
        Assert.Single((await Read<Page<ApplicationCard>>(await b.Api("applications?state=archived"))).Items);
        var restored = await Read<ApplicationView>(await b.Api($"applications/{a.Id}/restore", HttpMethod.Post, new VersionInput(archived.Version))); Assert.Equal("Accepted", restored.Status);
        Assert.Equal(10, (await Read<Page<HistoryView>>(await b.Api($"applications/{a.Id}/history"))).TotalCount);
    }
    [PostgreSqlFact]
    public async Task SimultaneousTransitionsHaveOneWinnerAndNoOrphanHistory()
    {
        await using var d = await Database(); using var h = new AuthFactory(d.ConnectionString); using var b = h.Browser(); await Register(b);
        var a = await Create(b, (await Job(b)).Id, "Applied");
        var results = await Task.WhenAll(b.Api($"applications/{a.Id}/status", HttpMethod.Post, new StatusInput("Interview", a.Version)), b.Api($"applications/{a.Id}/status", HttpMethod.Post, new StatusInput("Assessment", a.Version)));
        Assert.Single(results, r => r.StatusCode == HttpStatusCode.OK); Assert.Single(results, r => r.StatusCode == HttpStatusCode.Conflict);
        Assert.Equal(2, (await Read<Page<HistoryView>>(await b.Api($"applications/{a.Id}/history"))).TotalCount);
    }
    [PostgreSqlFact]
    public async Task ListsSearchFilterSortPaginateAndAggregateRealCounts()
    {
        await using var d = await Database(); using var h = new AuthFactory(d.ConnectionString); using var b = h.Browser(); await Register(b);
        var j = await Job(b, "Engineer", "Alpha"); var a = await Create(b, j.Id, "Applied"); h.Clock.Now = h.Clock.Now.AddSeconds(2); var z = await Create(b, (await Job(b, "Zoologist", "Beta")).Id);
        foreach (var filter in new[] { "search=engineer", "search=alpha", "status=Applied", $"companyId={j.CompanyId}", $"jobId={j.Id}", "appliedFrom=1900-01-01T00:00:00Z", "appliedTo=2100-01-01T00:00:00Z" })
            Assert.Equal(a.Id, Assert.Single((await Read<Page<ApplicationCard>>(await b.Api("applications?" + filter))).Items).Id);
        foreach (var (sort, id) in new[] { ("newest", z.Id), ("oldest", a.Id), ("updated", z.Id), ("applied", a.Id), ("company", a.Id), ("title", a.Id) })
            Assert.Equal(id, Assert.Single((await Read<Page<ApplicationCard>>(await b.Api($"applications?sort={sort}&pageSize=1"))).Items).Id);
        var page = await Read<Page<ApplicationCard>>(await b.Api("applications?sort=oldest&pageSize=1&page=2")); Assert.Equal(z.Id, Assert.Single(page.Items).Id); Assert.Equal(2, page.TotalPages);
        var summary = await Read<List<StageCount>>(await b.Api("applications/summary?state=all")); Assert.Equal(1, summary.Single(x => x.Status == "Saved").Count); Assert.Equal(1, summary.Single(x => x.Status == "Applied").Count); Assert.Equal(0, summary.Single(x => x.Status == "Offer").Count);
        var filtered = await Read<List<StageCount>>(await b.Api("applications/summary?search=alpha")); Assert.Equal(1, filtered.Sum(x => x.Count));
        foreach (var q in new[] { "page=0", "pageSize=101", "status=1", "sort=random", "state=deleted", "appliedFrom=2026-02-01T00:00:00Z&appliedTo=2026-01-01T00:00:00Z" })
            Assert.Equal(HttpStatusCode.BadRequest, (await b.Api("applications?" + q)).StatusCode);
        var payload = await (await b.Api("applications")).Content.ReadAsStringAsync(); Assert.DoesNotContain("description", payload, StringComparison.OrdinalIgnoreCase); Assert.DoesNotContain("history", payload, StringComparison.OrdinalIgnoreCase);
    }
    [PostgreSqlFact]
    public async Task NotesCrudConcurrencyPlainTextAndLimits()
    {
        await using var d = await Database(); using var h = new AuthFactory(d.ConnectionString); using var b = h.Browser(); await Register(b);
        var a = await Create(b, (await Job(b)).Id); var path = $"applications/{a.Id}/notes";
        var note = await Read<NoteView>(await b.Api(path, HttpMethod.Post, new NoteInput("<script>alert(1)</script>"))); Assert.Equal("<script>alert(1)</script>", note.Body);
        h.Clock.Now = h.Clock.Now.AddSeconds(3);
        var edited = await Read<NoteView>(await b.Api($"{path}/{note.Id}", HttpMethod.Put, new NoteInput("Updated", note.Version))); Assert.True(edited.UpdatedAtUtc > edited.CreatedAtUtc);
        Assert.Equal(HttpStatusCode.Conflict, (await b.Api($"{path}/{note.Id}", HttpMethod.Put, new NoteInput("Stale", note.Version))).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await b.Api($"{path}/{note.Id}?version={note.Version}", HttpMethod.Delete)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await b.Api(path, HttpMethod.Post, new NoteInput(" "))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await b.Api(path, HttpMethod.Post, new NoteInput(new string('x', 20001)))).StatusCode);
        await Read<NoteView>(await b.Api(path, HttpMethod.Post, new NoteInput(new string('漢', 20000))));
        Assert.Equal(HttpStatusCode.NoContent, (await b.Api($"{path}/{note.Id}?version={edited.Version}", HttpMethod.Delete)).StatusCode);
        Assert.Single((await Read<Page<NoteView>>(await b.Api(path))).Items);
    }
    [PostgreSqlFact]
    public async Task AllApplicationRelationshipsAndActionsEnforceTwoUserOwnership()
    {
        await using var d = await Database(); using var h = new AuthFactory(d.ConnectionString); using var a = h.Browser(); using var b = h.Browser(); await Register(a); await Register(b, "other@example.com");
        var foreignJob = await Job(b); var foreign = await Create(b, foreignJob.Id); var own = await Create(a, (await Job(a)).Id);
        var note = await Read<NoteView>(await b.Api($"applications/{foreign.Id}/notes", HttpMethod.Post, new NoteInput("Private")));
        foreach (var suffix in new[] { "", "/history", "/notes" }) Assert.Equal(HttpStatusCode.NotFound, (await a.Api($"applications/{foreign.Id}{suffix}")).StatusCode);
        foreach (var suffix in new[] { "archive", "restore" }) Assert.Equal(HttpStatusCode.NotFound, (await a.Api($"applications/{foreign.Id}/{suffix}", HttpMethod.Post, new VersionInput(foreign.Version))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await a.Api($"applications/{foreign.Id}/status", HttpMethod.Post, new StatusInput("Applied", foreign.Version))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await a.Api($"applications/{foreign.Id}", HttpMethod.Put, new EditApplicationInput(foreign.Version))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await a.Api($"applications/{foreign.Id}", HttpMethod.Delete)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await a.Api("applications", HttpMethod.Post, new CreateApplicationInput(foreignJob.Id))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await a.Api($"applications/{foreign.Id}/notes", HttpMethod.Post, new NoteInput("Attack"))).StatusCode);
        foreach (var parent in new[] { own.Id, foreign.Id })
        {
            Assert.Equal(HttpStatusCode.NotFound, (await a.Api($"applications/{parent}/notes/{note.Id}", HttpMethod.Put, new NoteInput("Attack", note.Version))).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await a.Api($"applications/{parent}/notes/{note.Id}?version={note.Version}", HttpMethod.Delete)).StatusCode);
        }
        Assert.Equal(own.Id, Assert.Single((await Read<Page<ApplicationCard>>(await a.Api("applications?state=all"))).Items).Id);
        Assert.Equal(1, (await Read<List<StageCount>>(await a.Api("applications/summary?state=all"))).Sum(x => x.Count));
        Guid ownedResume, foreignResume;
        await using (var db = d.Context())
        {
            var users = await db.Users.ToListAsync();
            Resume Add(Guid owner) { var doc = new Document(owner, "CV", "cv.pdf", Guid.NewGuid().ToString(), "application/pdf", 1, DocumentType.Resume); var r = new Resume(doc, "CV"); db.Documents.Add(doc); db.Resumes.Add(r); return r; }
            ownedResume = Add(users.Single(x => x.Email == "applications@example.com").Id).Id; foreignResume = Add(users.Single(x => x.Email == "other@example.com").Id).Id; await db.SaveChangesAsync();
        }
        Assert.Equal(HttpStatusCode.NotFound, (await a.Api($"applications/{own.Id}", HttpMethod.Put, new EditApplicationInput(own.Version, foreignResume))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await a.Api("applications", HttpMethod.Post, new CreateApplicationInput((await Job(a, "Other")).Id, ResumeId: foreignResume))).StatusCode);
        var attached = await Read<ApplicationView>(await a.Api($"applications/{own.Id}", HttpMethod.Put, new EditApplicationInput(own.Version, ownedResume))); Assert.Equal("CV", attached.ResumeName); Assert.Equal(ownedResume, attached.ResumeId);
        Assert.Equal(ownedResume, Assert.Single((await Read<Page<ResumeChoice>>(await a.Api("applications/resume-options"))).Items).Id);
    }
    [PostgreSqlFact]
    public async Task AuthenticationCsrfAndMassAssignmentRemainEnforced()
    {
        await using var d = await Database(); using var h = new AuthFactory(d.ConnectionString); using var b = h.Browser();
        Assert.Equal(HttpStatusCode.Unauthorized, (await b.Api("applications")).StatusCode); Assert.Equal(HttpStatusCode.Unauthorized, (await b.Api("applications/summary")).StatusCode);
        await Register(b); var job = await Job(b);
        Assert.Equal(HttpStatusCode.Forbidden, (await b.Api("applications", HttpMethod.Post, new CreateApplicationInput(job.Id), csrf: false)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await b.Api("applications", HttpMethod.Post, new { jobId = job.Id, userId = Guid.NewGuid() })).StatusCode);
        foreach (var status in new[] { "Interview", "1", "" }) Assert.Equal(HttpStatusCode.BadRequest, (await b.Api("applications", HttpMethod.Post, new CreateApplicationInput(job.Id, status))).StatusCode);
        Assert.Empty((await Read<Page<ApplicationCard>>(await b.Api("applications"))).Items);
    }
    [PostgreSqlFact]
    public async Task MigrationUpgradeRollbackReapplyAndQueriesAreBounded()
    {
        await using var d = await fixture.CreateDatabaseAsync();
        await using (var db = d.Context())
        {
            await db.GetService<IMigrator>().MigrateAsync("CompanyAndJobLifecycle"); await db.Database.MigrateAsync();
            await db.GetService<IMigrator>().MigrateAsync("CompanyAndJobLifecycle"); await db.Database.MigrateAsync(); Assert.False(db.Database.HasPendingModelChanges());
        }
        using var h = new AuthFactory(d.ConnectionString); using var b = h.Browser(); await Register(b); await Create(b, (await Job(b)).Id, "Applied");
        Guid owner; await using (var db = d.Context()) owner = await db.Users.Select(u => u.Id).SingleAsync();
        var commands = new List<string>();
        await using var context = new CareerOSDbContext(new DbContextOptionsBuilder<CareerOSDbContext>().UseNpgsql(d.ConnectionString).LogTo(commands.Add, [RelationalEventId.CommandExecuted]).Options);
        var service = new ApplicationService(context, new OwnedData(context, new Current(owner)), TimeProvider.System);
        await service.List(new(PageSize: 1, Search: "Engineer"), default); Assert.Equal(2, commands.Count);
        Assert.DoesNotContain(commands, s => s.Contains("Description") || s.Contains("application_status_history")); Assert.Contains(commands, s => s.Contains("LIMIT") && s.Contains("OFFSET") && s.Contains("ILIKE"));
        commands.Clear(); await service.Summary(new(), default); Assert.Single(commands); Assert.Contains("GROUP BY", commands[0]);
        // A downgrade must fail rather than inventing a Saved event for existing Applied-first history.
        await using var rollback = d.Context();
        await Assert.ThrowsAsync<Npgsql.PostgresException>(() => rollback.GetService<IMigrator>().MigrateAsync("CompanyAndJobLifecycle"));
        Assert.Equal(5, (await rollback.Database.GetAppliedMigrationsAsync()).Count());
    }
    private sealed record Current(Guid UserId) : ICurrentUser;
}
