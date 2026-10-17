using System.Net;
using System.Net.Http.Json;
using CareerOS.Application.Applications;
using CareerOS.Application.Dashboard;
using CareerOS.Application.Opportunities;
using CareerOS.Application.Planning;
using Microsoft.EntityFrameworkCore;
using Xunit;
namespace CareerOS.Tests.Integration;
[Collection("PostgreSQL")]
public sealed class DashboardTests(PostgreSqlFixture fixture)
{
    private static async Task<T> Read<T>(HttpResponseMessage r) { r.EnsureSuccessStatusCode(); return (await r.Content.ReadFromJsonAsync<T>())!; }
    private static async Task Register(Browser b, string email) => (await b.Send("register", new { email, password = "dashboard integration phrase", displayName = "Dashboard" })).EnsureSuccessStatusCode();
    private static async Task<ApplicationView> Application(Browser b) { var c = await Read<CompanyView>(await b.Api("companies", HttpMethod.Post, new CompanyInput("Owned company"))); var j = await Read<JobView>(await b.Api("jobs", HttpMethod.Post, new JobInput(c.Id, "Role"))); return await Read<ApplicationView>(await b.Api("applications", HttpMethod.Post, new CreateApplicationInput(j.Id))); }
    [PostgreSqlFact]
    public async Task CurrentOwnedDashboardIsTruthfulBoundedAndProtected()
    {
        await using var d = await fixture.CreateDatabaseAsync(); await using (var db = d.Context()) await db.Database.MigrateAsync(); using var h = new AuthFactory(d.ConnectionString); using var a = h.Browser(); using var b = h.Browser(); using var anonymous = h.Browser(); await Register(a, "dash-a@example.com"); await Register(b, "dash-b@example.com");
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.Api("dashboard")).StatusCode);
        var empty = await Read<DashboardView>(await b.Api("dashboard")); Assert.Equal(0, empty.TotalApplications); Assert.Empty(empty.NextTasks); Assert.Empty(empty.RecentApplications);
        var app = await Application(a); foreach (var s in new[] { "Applied", "Offer" }) app = await Read<ApplicationView>(await a.Api($"applications/{app.Id}/status", HttpMethod.Post, new StatusInput(s, app.Version)));
        for (var i = 0; i < 6; i++) await Application(a);
        var now = h.Clock.Now.UtcDateTime;
        await Read<InterviewView>(await a.Api("interviews", HttpMethod.Post, new InterviewInput(app.Id, "Technical", now.AddDays(1), now.AddDays(1).AddHours(1))));
        await Read<InterviewView>(await a.Api("interviews", HttpMethod.Post, new InterviewInput(app.Id, "Technical", now.AddHours(-1), now)));
        await Read<TaskView>(await a.Api("tasks", HttpMethod.Post, new TaskInput("Overdue", DueAtUtc: now.AddSeconds(-1))));
        await Read<TaskView>(await a.Api("tasks", HttpMethod.Post, new TaskInput("Open without date")));
        await Read<TaskView>(await a.Api("tasks", HttpMethod.Post, new TaskInput("Completed", Completed: true, DueAtUtc: now.AddDays(-1))));
        var v = await Read<DashboardView>(await a.Api("dashboard")); Assert.Equal(7, v.TotalApplications); Assert.Equal(7, v.ActiveApplications); Assert.Equal(1, v.Offers); Assert.Equal(7, v.SavedJobs); Assert.Equal(2, v.OpenTasks); Assert.Equal(1, v.OverdueTasks); Assert.Equal(1, v.UpcomingInterviews); Assert.Equal(5, v.RecentApplications.Count); Assert.Single(v.NextInterviews); Assert.Equal("Overdue", v.NextTasks[0].Title); Assert.Equal(2, v.NextTasks.Count);
        var other = await Read<DashboardView>(await b.Api("dashboard")); Assert.Equal(0, other.TotalApplications); Assert.Equal(0, other.UpcomingInterviews); Assert.Empty(other.RecentApplications); Assert.Empty(other.NextInterviews); Assert.Empty(other.NextTasks);
    }
}
