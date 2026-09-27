using System.Net;
using System.Net.Http.Json;
using CareerOS.Application.Opportunities;
using CareerOS.Application.Authentication;
using CareerOS.Domain.Applications;
using CareerOS.Infrastructure.Opportunities;
using CareerOS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Xunit;

namespace CareerOS.Tests.Integration;

[Collection("PostgreSQL")]
public sealed class OpportunityTests(PostgreSqlFixture fixture)
{
    private async Task<TestDatabase> Database()
    { var database = await fixture.CreateDatabaseAsync(); await using var db = database.Context(); await db.Database.MigrateAsync(); return database; }
    private static async Task Register(Browser b, string email = "jobs@example.com") => (await b.Send("register", new { email, password = "opportunity test passphrase", displayName = "Test person" })).EnsureSuccessStatusCode();
    private static async Task<T> Read<T>(HttpResponseMessage r) { r.EnsureSuccessStatusCode(); return (await r.Content.ReadFromJsonAsync<T>())!; }
    private static async Task<CompanyView> CreateCompany(Browser b, string name = "Example", string? location = "Riga") => await Read<CompanyView>(await b.Api("companies", HttpMethod.Post, new CompanyInput(name, Industry: "Software", Location: location)));
    private static async Task<JobView> CreateJob(Browser b, Guid company, string title = "Engineer") => await Read<JobView>(await b.Api("jobs", HttpMethod.Post, new JobInput(company, title, Location: "Riga", WorkMode: "Remote", EmploymentType: "FullTime", SeniorityLevel: "Senior", Source: "Careers", SourceUrl: "https://example.com/job", SalaryMin: 1000, SalaryMax: 2000, SalaryCurrency: "eur", SalaryPeriod: "Month", PostedAtUtc: DateTime.Parse("2026-01-01T00:00:00Z").ToUniversalTime())));
    [PostgreSqlFact]
    public async Task AnonymousRequestsAndMassAssignmentAndCsrfAreRejected()
    {
        await using var database = await Database(); using var host = new AuthFactory(database.ConnectionString); using var b = host.Browser();
        foreach (var resource in new[] { "companies", "jobs" })
        {
            foreach (var method in new[] { HttpMethod.Get, HttpMethod.Post, HttpMethod.Put, HttpMethod.Delete })
                Assert.Equal(HttpStatusCode.Unauthorized, (await b.Api(resource + (method == HttpMethod.Put || method == HttpMethod.Delete ? $"/{Guid.NewGuid()}?version=1" : ""), method, method == HttpMethod.Post || method == HttpMethod.Put ? new { } : null)).StatusCode);
        }
        await Register(b);
        Assert.Equal(HttpStatusCode.BadRequest, (await b.Api("companies", HttpMethod.Post, new { name = "Unsafe", userId = Guid.NewGuid() })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await b.Api("companies", HttpMethod.Post, new CompanyInput("Name"), csrf: false)).StatusCode);
    }
    [PostgreSqlFact]
    public async Task CompanyCrudDuplicatesQueriesAndAuditPersist()
    {
        await using var database = await Database(); using var host = new AuthFactory(database.ConnectionString); using var b = host.Browser(); await Register(b);
        var c = await CreateCompany(b, " Alpha "); await CreateCompany(b, "Alpha"); await CreateCompany(b, "Beta", "Oslo"); await CreateCompany(b, "Percent%Co");
        Assert.Equal("Alpha", c.Name); var fetched = await Read<CompanyView>(await b.Api($"companies/{c.Id}")); Assert.Equal(c with { CreatedAtUtc = fetched.CreatedAtUtc, UpdatedAtUtc = fetched.UpdatedAtUtc }, fetched); Assert.Equal(c.CreatedAtUtc.Ticks / 10, fetched.CreatedAtUtc.Ticks / 10);
        var page = await Read<Page<CompanySummary>>(await b.Api("companies?search=alpha&pageSize=1&page=2&industry=soft&location=riga")); Assert.Single(page.Items); Assert.Equal(2, page.TotalCount); Assert.Equal(2, page.PageNumber);
        Assert.Single((await Read<Page<CompanySummary>>(await b.Api("companies?search=%25"))).Items);
        Assert.Equal(4, (await Read<Page<CompanySummary>>(await b.Api("companies?sort=oldest"))).TotalCount);
        host.Clock.Now = host.Clock.Now.AddSeconds(3);
        var saved = await Read<CompanyView>(await b.Api($"companies/{c.Id}", HttpMethod.Put, new CompanyInput("Renamed", Website: "https://example.com", Version: c.Version)));
        Assert.Equal(c.CreatedAtUtc.Ticks / 10, saved.CreatedAtUtc.Ticks / 10); Assert.True(saved.UpdatedAtUtc > c.UpdatedAtUtc);
        Assert.Equal(HttpStatusCode.Conflict, (await b.Api($"companies/{c.Id}?version={c.Version}", HttpMethod.Delete)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await b.Api($"companies/{c.Id}?version={saved.Version}", HttpMethod.Delete)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await b.Api($"companies/{c.Id}")).StatusCode);
    }
    [PostgreSqlFact]
    public async Task JobsQueryAllFiltersAndSortsAndPagination()
    {
        await using var database = await Database(); using var host = new AuthFactory(database.ConnectionString); using var b = host.Browser(); await Register(b);
        var c = await CreateCompany(b, "Alpha"); var other = await CreateCompany(b, "Beta");
        var a = await CreateJob(b, c.Id, "Engineer"); host.Clock.Now = host.Clock.Now.AddSeconds(2); var z = await CreateJob(b, other.Id, "Zoologist");
        await Read<JobView>(await b.Api($"jobs/{z.Id}", HttpMethod.Put, new JobInput(other.Id, "Zoologist", Location: "Oslo", WorkMode: "OnSite", EmploymentType: "Contract", SeniorityLevel: "Entry", Source: "Referral", Version: z.Version)));
        foreach (var filter in new[] { "search=engineer", "search=alpha", "search=riga", $"companyId={c.Id}", "location=riga", "workMode=Remote", "employmentType=FullTime", "seniorityLevel=Senior", "source=careers" })
            Assert.Equal(a.Id, Assert.Single((await Read<Page<JobSummary>>(await b.Api("jobs?" + filter))).Items).Id);
        foreach (var (sort, id) in new[] { ("newest", z.Id), ("oldest", a.Id), ("title", a.Id), ("company", a.Id), ("posted", a.Id) })
            Assert.Equal(id, Assert.Single((await Read<Page<JobSummary>>(await b.Api($"jobs?sort={sort}&pageSize=1"))).Items).Id);
        var page = await Read<Page<JobSummary>>(await b.Api("jobs?page=2&pageSize=1&sort=oldest")); Assert.Equal(z.Id, Assert.Single(page.Items).Id); Assert.Equal(2, page.TotalPages);
        Assert.DoesNotContain("description", await (await b.Api("jobs")).Content.ReadAsStringAsync(), StringComparison.OrdinalIgnoreCase);
        foreach (var q in new[] { "page=0", "pageSize=101", "sort=unsupported", "workMode=1", "state=deleted", "companyId=invalid" }) Assert.Equal(HttpStatusCode.BadRequest, (await b.Api("jobs?" + q)).StatusCode);
    }
    [PostgreSqlFact]
    public async Task JobsPersistEditArchiveRestoreAndDeleteWithVersionAndAudit()
    {
        await using var database = await Database(); using var host = new AuthFactory(database.ConnectionString); using var b = host.Browser(); await Register(b);
        var c = await CreateCompany(b); var j = await CreateJob(b, c.Id); Assert.Equal("EUR", j.SalaryCurrency); var fetched = await Read<JobView>(await b.Api($"jobs/{j.Id}")); Assert.Equal(j with { CreatedAtUtc = fetched.CreatedAtUtc, UpdatedAtUtc = fetched.UpdatedAtUtc }, fetched); Assert.Equal(j.CreatedAtUtc.Ticks / 10, fetched.CreatedAtUtc.Ticks / 10);
        host.Clock.Now = host.Clock.Now.AddSeconds(2);
        var updated = await Read<JobView>(await b.Api($"jobs/{j.Id}", HttpMethod.Put, new JobInput(c.Id, "Updated", Description: "Full details", Version: j.Version)));
        Assert.True(updated.UpdatedAtUtc > j.UpdatedAtUtc); Assert.Equal(j.CreatedAtUtc.Ticks / 10, updated.CreatedAtUtc.Ticks / 10);
        Assert.Equal(HttpStatusCode.Conflict, (await b.Api($"jobs/{j.Id}?version={j.Version}", HttpMethod.Delete)).StatusCode);
        var archived = await Read<JobView>(await b.Api($"jobs/{j.Id}/archive", HttpMethod.Put, new ArchiveInput(true, updated.Version)));
        Assert.NotNull(archived.ArchivedAtUtc); Assert.Empty((await Read<Page<JobSummary>>(await b.Api("jobs"))).Items); Assert.Single((await Read<Page<JobSummary>>(await b.Api("jobs?state=archived"))).Items);
        Assert.Equal(HttpStatusCode.Conflict, (await b.Api($"jobs/{j.Id}/archive", HttpMethod.Put, new ArchiveInput(false, updated.Version))).StatusCode);
        var restored = await Read<JobView>(await b.Api($"jobs/{j.Id}/archive", HttpMethod.Put, new ArchiveInput(false, archived.Version))); Assert.Null(restored.ArchivedAtUtc);
        Assert.Equal(HttpStatusCode.NoContent, (await b.Api($"jobs/{j.Id}?version={restored.Version}", HttpMethod.Delete)).StatusCode);
    }
    [PostgreSqlFact]
    public async Task ForeignResourcesAndCompanyReferencesAreNotFound()
    {
        await using var database = await Database(); using var host = new AuthFactory(database.ConnectionString); using var a = host.Browser(); using var b = host.Browser(); await Register(a); await Register(b, "other@example.com");
        var c = await CreateCompany(b); var j = await CreateJob(b, c.Id);
        foreach (var (resource, id, version, input) in new (string, Guid, uint, object)[] { ("companies", c.Id, c.Version, new CompanyInput("Attack", Version: c.Version)), ("jobs", j.Id, j.Version, new JobInput(c.Id, "Attack", Version: j.Version)) })
        {
            Assert.Equal(HttpStatusCode.NotFound, (await a.Api($"{resource}/{id}")).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await a.Api($"{resource}/{id}", HttpMethod.Put, input)).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await a.Api($"{resource}/{id}?version={version}", HttpMethod.Delete)).StatusCode);
        }
        Assert.Empty((await Read<Page<CompanySummary>>(await a.Api("companies"))).Items); Assert.Empty((await Read<Page<JobSummary>>(await a.Api("jobs?state=all"))).Items);
        Assert.Equal(HttpStatusCode.NotFound, (await a.Api("jobs", HttpMethod.Post, new JobInput(c.Id, "Attack"))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await a.Api($"jobs/{j.Id}/archive", HttpMethod.Put, new ArchiveInput(true, j.Version))).StatusCode);
        var own = await CreateCompany(a); var ownJob = await CreateJob(a, own.Id);
        Assert.Equal(HttpStatusCode.NotFound, (await a.Api($"jobs/{ownJob.Id}", HttpMethod.Put, new JobInput(c.Id, "Attack", Version: ownJob.Version))).StatusCode);
    }
    [PostgreSqlFact]
    public async Task ReferencedCompanyAndJobCannotBeDeletedButJobCanBeArchived()
    {
        await using var database = await Database(); using var host = new AuthFactory(database.ConnectionString); using var b = host.Browser(); await Register(b);
        var c = await CreateCompany(b); var j = await CreateJob(b, c.Id);
        Assert.Equal(HttpStatusCode.Conflict, (await b.Api($"companies/{c.Id}?version={c.Version}", HttpMethod.Delete)).StatusCode);
        await using (var db = database.Context()) { db.Applications.Add(new JobApplication(await db.Jobs.SingleAsync())); await db.SaveChangesAsync(); }
        Assert.Equal(HttpStatusCode.Conflict, (await b.Api($"jobs/{j.Id}?version={j.Version}", HttpMethod.Delete)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await b.Api($"jobs/{j.Id}/archive", HttpMethod.Put, new ArchiveInput(true, j.Version))).StatusCode);
        await using var check = database.Context(); Assert.Single(await check.Applications.ToListAsync()); Assert.Single(await check.ApplicationStatusHistory.ToListAsync());
    }
    [PostgreSqlFact]
    public async Task InvalidDataNeverPersistsAndConcurrentEditsHaveOneWinner()
    {
        await using var database = await Database(); using var host = new AuthFactory(database.ConnectionString); using var b = host.Browser(); await Register(b); var c = await CreateCompany(b);
        foreach (var input in new[] { new JobInput(c.Id, "", SalaryMin: 1), new JobInput(c.Id, "Bad", SalaryMin: -1), new JobInput(c.Id, "Bad", SourceUrl: "javascript:bad"), new JobInput(c.Id, "Bad", EmploymentType: "invalid") }) Assert.Equal(HttpStatusCode.BadRequest, (await b.Api("jobs", HttpMethod.Post, input)).StatusCode);
        Assert.Empty((await Read<Page<JobSummary>>(await b.Api("jobs"))).Items);
        var j = await CreateJob(b, c.Id);
        var edits = await Task.WhenAll(b.Api($"jobs/{j.Id}", HttpMethod.Put, new JobInput(c.Id, "One", Version: j.Version)), b.Api($"jobs/{j.Id}", HttpMethod.Put, new JobInput(c.Id, "Two", Version: j.Version)));
        Assert.Single(edits, x => x.StatusCode == HttpStatusCode.OK); Assert.Single(edits, x => x.StatusCode == HttpStatusCode.Conflict);
        var companies = await Task.WhenAll(b.Api($"companies/{c.Id}", HttpMethod.Put, new CompanyInput("One", Version: c.Version)), b.Api($"companies/{c.Id}", HttpMethod.Put, new CompanyInput("Two", Version: c.Version)));
        Assert.Single(companies, x => x.StatusCode == HttpStatusCode.OK); Assert.Single(companies, x => x.StatusCode == HttpStatusCode.Conflict);
    }
    [PostgreSqlFact]
    public async Task LifecycleMigrationPreservesExistingJobsAndRollsBackAndReapplies()
    {
        await using var database = await fixture.CreateDatabaseAsync(); await using var db = database.Context();
        await db.GetService<IMigrator>().MigrateAsync("CareerProfilePreferences");
        var owner = Guid.NewGuid(); var company = Guid.NewGuid(); var job = Guid.NewGuid();
        await database.ExecuteAsync($"INSERT INTO users (\"Id\",\"Email\",\"DisplayName\") VALUES ('{owner}','migration@example.com','Test'); INSERT INTO companies (\"Id\",\"UserId\",\"Name\") VALUES ('{company}','{owner}','Existing'); INSERT INTO jobs (\"Id\",\"UserId\",\"CompanyId\",\"Title\") VALUES ('{job}','{owner}','{company}','Existing role')");
        await db.Database.MigrateAsync(); var saved = await db.Jobs.SingleAsync(); Assert.Null(saved.ArchivedAtUtc); Assert.Equal("Existing role", saved.Title);
        await db.GetService<IMigrator>().MigrateAsync("CareerProfilePreferences"); Assert.Equal("Existing role", await database.ScalarAsync<string>("SELECT \"Title\" FROM jobs"));
        await db.Database.MigrateAsync(); Assert.False(db.Database.HasPendingModelChanges());
    }

    [PostgreSqlFact]
    public async Task ListsProjectOnlyNeededColumnsWithTwoBoundedDatabaseQueries()
    {
        await using var database = await Database(); using var host = new AuthFactory(database.ConnectionString); using var b = host.Browser(); await Register(b);
        var c = await CreateCompany(b); await CreateJob(b, c.Id);
        Guid owner; await using (var original = database.Context()) owner = await original.Users.Select(u => u.Id).SingleAsync();
        var commands = new List<string>();
        await using var db = new CareerOSDbContext(new DbContextOptionsBuilder<CareerOSDbContext>().UseNpgsql(database.ConnectionString)
            .LogTo(commands.Add, [RelationalEventId.CommandExecuted]).Options);
        var current = new Current(owner); var service = new OpportunityService(db, new OwnedData(db, current), current, TimeProvider.System);
        await service.Companies(new(PageSize: 1), default);
        Assert.Equal(2, commands.Count); Assert.DoesNotContain(commands, sql => sql.Contains("Description")); Assert.Contains(commands, sql => sql.Contains("LIMIT") && sql.Contains("OFFSET"));
        commands.Clear(); await service.Jobs(new(PageSize: 1, Search: "engineer"), default);
        Assert.Equal(2, commands.Count); Assert.DoesNotContain(commands, sql => sql.Contains("Description")); Assert.Contains(commands, sql => sql.Contains("JOIN") && sql.Contains("LIMIT") && sql.Contains("ILIKE"));
    }
    private sealed record Current(Guid UserId) : ICurrentUser;
}
