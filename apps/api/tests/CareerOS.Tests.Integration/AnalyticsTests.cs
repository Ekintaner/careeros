using System.Net;
using System.Net.Http.Json;
using CareerOS.Application.Analytics;
using CareerOS.Application.Applications;
using CareerOS.Application.Opportunities;
using CareerOS.Application.Planning;
using Microsoft.EntityFrameworkCore;
using Xunit;
namespace CareerOS.Tests.Integration;
[Collection("PostgreSQL")]
public sealed class AnalyticsTests(PostgreSqlFixture fixture)
{
    [PostgreSqlFact]
    public async Task AggregatesAreRealOwnerScopedAndCalendarFiltered()
    {
        await using var d = await fixture.CreateDatabaseAsync(); await using (var db = d.Context()) await db.Database.MigrateAsync();
        using var h = new AuthFactory(d.ConnectionString); using var a = h.Browser(); using var b = h.Browser();
        foreach (var (browser, email) in new[] { (a, "analytics@example.com"), (b, "other@example.com") }) (await browser.Send("register", new { email, password = "analytics test passphrase", displayName = "Tester" })).EnsureSuccessStatusCode();
        async Task<T> Read<T>(HttpResponseMessage r) { r.EnsureSuccessStatusCode(); return (await r.Content.ReadFromJsonAsync<T>())!; }
        var c = await Read<CompanyView>(await a.Api("companies", HttpMethod.Post, new CompanyInput("Metrics Co")));
        var j = await Read<JobView>(await a.Api("jobs", HttpMethod.Post, new JobInput(c.Id, "Engineer")));
        var app = await Read<ApplicationView>(await a.Api("applications", HttpMethod.Post, new CreateApplicationInput(j.Id)));
        await Read<InterviewView>(await a.Api("interviews", HttpMethod.Post, new InterviewInput(app.Id, "Technical", h.Clock.Now.UtcDateTime, h.Clock.Now.UtcDateTime.AddHours(1))));
        await Read<TaskView>(await a.Api("tasks", HttpMethod.Post, new TaskInput("Follow up", DueAtUtc: h.Clock.Now.UtcDateTime.AddDays(-1))));

        var v = await Read<AnalyticsView>(await a.Api("analytics?timeZone=America%2FNew_York"));
        Assert.Equal(1, v.Total); Assert.Equal(0, v.Applied); Assert.Equal(0, v.Interviewed); Assert.Equal(1, v.Trends.Sum(x => x.Count)); Assert.Equal("Metrics Co", Assert.Single(v.Companies).Label); Assert.Equal("Engineer", Assert.Single(v.Roles).Label); Assert.Equal("Overdue", Assert.Single(v.Tasks).Label); Assert.Equal(1, Assert.Single(v.Interviews).Count); Assert.Empty(v.Durations);
        foreach (var status in new[] { "Applied", "Interview", "Offer", "Interview", "Offer", "Accepted" })
        {
            h.Clock.Now = h.Clock.Now.AddDays(1);
            app = await Read<ApplicationView>(await a.Api($"applications/{app.Id}/status", HttpMethod.Post, new StatusInput(status, app.Version)));
        }
        var progressed = await Read<AnalyticsView>(await a.Api("analytics")); Assert.Equal(1, progressed.Applied); Assert.Equal(1, progressed.Interviewed); Assert.Equal(1, progressed.Offered);
        Assert.Equal(2, progressed.Durations.Single(x => x.Label == "Interview").Count); Assert.Equal(1, progressed.Durations.Single(x => x.Label == "Interview").AverageDays); Assert.DoesNotContain(progressed.Durations, x => x.Label == "Accepted");
        var foreign = await Read<AnalyticsView>(await b.Api("analytics")); Assert.Equal(0, foreign.Total); Assert.Empty(foreign.Companies); Assert.Empty(foreign.Tasks); Assert.Empty(foreign.Interviews);
        var empty = await Read<AnalyticsView>(await a.Api("analytics?from=2000-01-01&to=2000-01-01&interval=month")); Assert.Equal(0, empty.Total); Assert.Equal(0, Assert.Single(empty.Trends).Count);
        Assert.Equal(HttpStatusCode.BadRequest, (await a.Api("analytics?timeZone=invalid")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await a.Api("analytics?from=2026-02-01&to=2026-01-01")).StatusCode);
    }
    [PostgreSqlFact]
    public async Task LocalCalendarDstBoundariesAreInclusiveExclusiveOnPostgres()
    {
        await using var d = await fixture.CreateDatabaseAsync(); await using (var db = d.Context()) await db.Database.MigrateAsync();
        using var h = new AuthFactory(d.ConnectionString); using var b = h.Browser(); using var anonymous = h.Browser();
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.Api("analytics")).StatusCode);

        (await b.Send("register", new { email = "dst@example.com", password = "calendar test passphrase", displayName = "Calendar" })).EnsureSuccessStatusCode();
        async Task<T> Read<T>(HttpResponseMessage r) { r.EnsureSuccessStatusCode(); return (await r.Content.ReadFromJsonAsync<T>())!; }
        var c = await Read<CompanyView>(await b.Api("companies", HttpMethod.Post, new CompanyInput("Calendar Co")));
        foreach (var time in new[] { "2026-03-08T04:59:59Z", "2026-03-08T05:00:00Z", "2026-03-09T03:59:59Z", "2026-03-09T04:00:00Z" })
        {
            var clock = new TestClock { Now = DateTimeOffset.Parse(time) };
            await using var db = d.Context(clock);
            var company = await db.Companies.SingleAsync(x => x.Id == c.Id);
            var job = new CareerOS.Domain.Opportunities.Job(company, time); db.Jobs.Add(job);
            db.Applications.Add(new CareerOS.Domain.Applications.JobApplication(job, clock, CareerOS.Domain.Applications.ApplicationStatus.Applied));
            await db.SaveChangesAsync();
        }
        var v = await Read<AnalyticsView>(await b.Api("analytics?from=2026-03-08&to=2026-03-08&timeZone=America%2FNew_York"));
        Assert.Equal(2, v.Total); Assert.Equal(2, v.Applied); Assert.Equal(2, Assert.Single(v.Trends).Count); Assert.Equal(23, (v.EndExclusiveUtc - v.StartUtc).TotalHours); Assert.Equal(2, v.Roles.Count);
        Assert.Equal(HttpStatusCode.BadRequest, (await b.Api("analytics?from=bad")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await b.Api("analytics?timeZone=Eastern%20Standard%20Time")).StatusCode);
    }
}
