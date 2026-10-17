using System.Net;
using System.Net.Http.Json;
using CareerOS.Application.Applications;
using CareerOS.Application.Opportunities;
using CareerOS.Application.Planning;
using Microsoft.EntityFrameworkCore;
using Xunit;
namespace CareerOS.Tests.Integration;
[Collection("PostgreSQL")]
public sealed class PlanningTests(PostgreSqlFixture fixture)
{
    private async Task<TestDatabase> Database() { var d = await fixture.CreateDatabaseAsync(); await using var db = d.Context(); await db.Database.MigrateAsync(); return d; }
    private static async Task<T> Read<T>(HttpResponseMessage r) { r.EnsureSuccessStatusCode(); return (await r.Content.ReadFromJsonAsync<T>())!; }
    private static async Task Register(Browser b, string email = "planning@example.com") => (await b.Send("register", new { email, password = "planning test passphrase", displayName = "Test person" })).EnsureSuccessStatusCode();
    private static async Task<ApplicationView> Application(Browser b)
    {
        var c = await Read<CompanyView>(await b.Api("companies", HttpMethod.Post, new CompanyInput("Company")));
        var j = await Read<JobView>(await b.Api("jobs", HttpMethod.Post, new JobInput(c.Id, "Role")));
        return await Read<ApplicationView>(await b.Api("applications", HttpMethod.Post, new CreateApplicationInput(j.Id)));
    }
    private static InterviewInput Input(Guid a, DateTime now, uint version = 0) => new(a, "Technical", now, now.AddHours(1), "Remote", "https://example.com/meeting", Version: version);
    [PostgreSqlFact]
    public async Task InterviewAndNotesCrudPersistWithConcurrencyAndRestrictedDelete()
    {
        await using var d = await Database(); using var h = new AuthFactory(d.ConnectionString); using var b = h.Browser(); await Register(b); var a = await Application(b);
        var input = Input(a.Id, h.Clock.Now.UtcDateTime.AddDays(1));
        var x = await Read<InterviewView>(await b.Api("interviews", HttpMethod.Post, input)); Assert.Equal(input.ScheduledStartUtc.Ticks / 10, x.ScheduledStartUtc.Ticks / 10);
        h.Clock.Now = h.Clock.Now.AddSeconds(1);
        var edited = await Read<InterviewView>(await b.Api($"interviews/{x.Id}", HttpMethod.Put, input with { Type = "Final", Outcome = "Prepare portfolio", Version = x.Version }));
        Assert.Equal("Final", edited.Type); Assert.True(edited.UpdatedAtUtc > edited.CreatedAtUtc);
        Assert.Equal(HttpStatusCode.Conflict, (await b.Api($"interviews/{x.Id}", HttpMethod.Put, input with { Version = x.Version })).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await b.Api($"interviews/{x.Id}?version={x.Version}", HttpMethod.Delete)).StatusCode);
        var path = $"interviews/{x.Id}/notes";
        var note = await Read<NoteView>(await b.Api(path, HttpMethod.Post, new NoteInput("<script>private</script>")));
        Assert.Equal(HttpStatusCode.Conflict, (await b.Api($"interviews/{x.Id}?version={edited.Version}", HttpMethod.Delete)).StatusCode);
        var updated = await Read<NoteView>(await b.Api($"{path}/{note.Id}", HttpMethod.Put, new NoteInput("Updated", note.Version)));
        Assert.Equal(HttpStatusCode.Conflict, (await b.Api($"{path}/{note.Id}?version={note.Version}", HttpMethod.Delete)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await b.Api($"{path}/{note.Id}", HttpMethod.Put, new NoteInput("Stale", note.Version))).StatusCode);
        Assert.Equal("Updated", Assert.Single((await Read<Page<NoteView>>(await b.Api(path))).Items).Body);
        await Read<NoteView>(await b.Api(path, HttpMethod.Post, new NoteInput(new string('漢', 20000))));
        Assert.Equal(HttpStatusCode.BadRequest, (await b.Api(path, HttpMethod.Post, new NoteInput(new string('x', 20001)))).StatusCode);
        foreach (var n in (await Read<Page<NoteView>>(await b.Api(path))).Items) Assert.Equal(HttpStatusCode.NoContent, (await b.Api($"{path}/{n.Id}?version={n.Version}", HttpMethod.Delete)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await b.Api($"interviews/{x.Id}?version={edited.Version}", HttpMethod.Delete)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await b.Api($"interviews/{x.Id}")).StatusCode);
        await using var db = d.Context(); Assert.Empty(await db.Interviews.ToListAsync()); Assert.Empty(await db.InterviewNotes.ToListAsync()); Assert.False(db.Database.HasPendingModelChanges());
    }
    [PostgreSqlFact]
    public async Task TasksCrudOptionalLinksDueDatesCompletionAndReopen()
    {
        await using var d = await Database(); using var h = new AuthFactory(d.ConnectionString); using var b = h.Browser(); await Register(b); var a = await Application(b);
        var input = new TaskInput("Follow up", "Ask about next steps", h.Clock.Now.UtcDateTime.AddDays(-1), "High", ApplicationId: a.Id);
        var x = await Read<TaskView>(await b.Api("tasks", HttpMethod.Post, input)); Assert.Null(x.CompletedAtUtc);
        var edited = await Read<TaskView>(await b.Api($"tasks/{x.Id}", HttpMethod.Put, input with { Completed = true, Version = x.Version })); Assert.NotNull(edited.CompletedAtUtc);
        Assert.Empty((await Read<Page<TaskView>>(await b.Api("tasks?view=overdue"))).Items); Assert.Single((await Read<Page<TaskView>>(await b.Api("tasks?view=completed"))).Items);
        Assert.Equal(HttpStatusCode.Conflict, (await b.Api($"tasks/{x.Id}", HttpMethod.Put, input with { Version = x.Version })).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await b.Api($"tasks/{x.Id}?version={x.Version}", HttpMethod.Delete)).StatusCode);
        var reopened = await Read<TaskView>(await b.Api($"tasks/{x.Id}", HttpMethod.Put, input with { Version = edited.Version, ApplicationId = null })); Assert.Null(reopened.CompletedAtUtc); Assert.Null(reopened.ApplicationId);
        Assert.Equal(HttpStatusCode.NoContent, (await b.Api($"tasks/{x.Id}?version={reopened.Version}", HttpMethod.Delete)).StatusCode);
        await using var db = d.Context(); Assert.Empty(await db.CareerTasks.ToListAsync());
    }
    [PostgreSqlFact]
    public async Task TwoUsersCannotReadMutateOrLinkEachOthersPlanningData()
    {
        await using var d = await Database(); using var h = new AuthFactory(d.ConnectionString); using var a = h.Browser(); using var b = h.Browser(); await Register(a); await Register(b, "other@example.com");
        var foreign = await Application(b); var own = await Application(a); var input = Input(foreign.Id, h.Clock.Now.UtcDateTime.AddDays(1));
        var interview = await Read<InterviewView>(await b.Api("interviews", HttpMethod.Post, input)); var path = $"interviews/{interview.Id}";
        var note = await Read<NoteView>(await b.Api(path + "/notes", HttpMethod.Post, new NoteInput("Private")));
        var task = await Read<TaskView>(await b.Api("tasks", HttpMethod.Post, new TaskInput("Private task", ApplicationId: foreign.Id)));
        var ownInterview = await Read<InterviewView>(await a.Api("interviews", HttpMethod.Post, Input(own.Id, h.Clock.Now.UtcDateTime)));
        foreach (var route in new[] { path, path + "/notes", $"tasks/{task.Id}" }) Assert.Equal(HttpStatusCode.NotFound, (await a.Api(route)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await a.Api(path, HttpMethod.Put, input with { Version = interview.Version })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await a.Api(path + $"?version={interview.Version}", HttpMethod.Delete)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await a.Api("interviews", HttpMethod.Post, input)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await a.Api("tasks", HttpMethod.Post, new TaskInput("Attack", ApplicationId: foreign.Id))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await a.Api($"tasks/{task.Id}", HttpMethod.Put, new TaskInput("Attack", Version: task.Version))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await a.Api($"tasks/{task.Id}?version={task.Version}", HttpMethod.Delete)).StatusCode);
        foreach (var parent in new[] { interview.Id, ownInterview.Id })
        {
            Assert.Equal(HttpStatusCode.NotFound, (await a.Api($"interviews/{parent}/notes/{note.Id}", HttpMethod.Put, new NoteInput("Attack", note.Version))).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await a.Api($"interviews/{parent}/notes/{note.Id}?version={note.Version}", HttpMethod.Delete)).StatusCode);
        }
        Assert.Equal(HttpStatusCode.NotFound, (await a.Api(path + "/notes", HttpMethod.Post, new NoteInput("Attack"))).StatusCode);
        var ownTask = await Read<TaskView>(await a.Api("tasks", HttpMethod.Post, new TaskInput("Own task")));
        Assert.Equal(HttpStatusCode.NotFound, (await a.Api($"tasks/{ownTask.Id}", HttpMethod.Put, new TaskInput("Attack", ApplicationId: foreign.Id, Version: ownTask.Version))).StatusCode);
        Assert.Single((await Read<Page<InterviewView>>(await a.Api("interviews?view=all"))).Items); Assert.Single((await Read<Page<TaskView>>(await a.Api("tasks?view=all"))).Items);
        Assert.Empty((await Read<Page<InterviewView>>(await a.Api($"interviews?applicationId={foreign.Id}"))).Items);
        Assert.Empty((await Read<Page<TaskView>>(await a.Api($"tasks?applicationId={foreign.Id}"))).Items);
    }
    [PostgreSqlFact]
    public async Task ConcurrentEditsHaveOneWinnerForInterviewsTasksAndNotes()
    {
        await using var d = await Database(); using var h = new AuthFactory(d.ConnectionString); using var b = h.Browser(); await Register(b); var a = await Application(b);
        var input = Input(a.Id, h.Clock.Now.UtcDateTime);
        var x = await Read<InterviewView>(await b.Api("interviews", HttpMethod.Post, input));
        var t = await Read<TaskView>(await b.Api("tasks", HttpMethod.Post, new TaskInput("Prepare")));
        var n = await Read<NoteView>(await b.Api($"interviews/{x.Id}/notes", HttpMethod.Post, new NoteInput("Original")));
        var paths = new[] { $"interviews/{x.Id}", $"tasks/{t.Id}", $"interviews/{x.Id}/notes/{n.Id}" };
        object[] first = [input with { Version = x.Version, Location = "A" }, new TaskInput("A", Version: t.Version), new NoteInput("A", n.Version)];
        object[] second = [input with { Version = x.Version, Location = "B" }, new TaskInput("B", Version: t.Version), new NoteInput("B", n.Version)];
        for (var i = 0; i < paths.Length; i++)
        {
            var results = await System.Threading.Tasks.Task.WhenAll(b.Api(paths[i], HttpMethod.Put, first[i]), b.Api(paths[i], HttpMethod.Put, second[i]));
            Assert.Single(results, r => r.StatusCode == HttpStatusCode.OK); Assert.Single(results, r => r.StatusCode == HttpStatusCode.Conflict);
        }
    }
    [PostgreSqlFact]
    public async Task UpcomingOverdueAndPastViewsAreOwnerScopedBoundedAndOrdered()
    {
        await using var d = await Database(); using var h = new AuthFactory(d.ConnectionString); using var b = h.Browser(); await Register(b); var a = await Application(b); var now = h.Clock.Now.UtcDateTime;
        await Read<InterviewView>(await b.Api("interviews", HttpMethod.Post, Input(a.Id, now.AddHours(-2))));
        var ongoing = await Read<InterviewView>(await b.Api("interviews", HttpMethod.Post, Input(a.Id, now.AddMinutes(-10))));
        await Read<InterviewView>(await b.Api("interviews", HttpMethod.Post, Input(a.Id, now.AddDays(1))));
        var page = await Read<Page<InterviewView>>(await b.Api("interviews?pageSize=1")); Assert.Equal(2, page.TotalCount); Assert.Equal(ongoing.Id, Assert.Single(page.Items).Id); Assert.Equal(2, page.TotalPages);
        Assert.Single((await Read<Page<InterviewView>>(await b.Api("interviews?view=past"))).Items);
        foreach (var due in new DateTime?[] { now.AddDays(-1), now, now.AddDays(7), now.AddDays(8), null }) await Read<TaskView>(await b.Api("tasks", HttpMethod.Post, new TaskInput("Prepare", DueAtUtc: due)));
        Assert.Single((await Read<Page<TaskView>>(await b.Api("tasks?view=overdue"))).Items); Assert.Equal(2, (await Read<Page<TaskView>>(await b.Api("tasks?view=upcoming"))).TotalCount);
        Assert.Equal(5, (await Read<Page<TaskView>>(await b.Api("tasks"))).TotalCount); Assert.Null((await Read<Page<TaskView>>(await b.Api("tasks?view=all&page=5&pageSize=1"))).Items.Single().DueAtUtc);
        foreach (var root in new[] { "tasks", "interviews" }) foreach (var query in new[] { "page=0", "pageSize=101", "view=invalid", "page=10001" }) Assert.Equal(HttpStatusCode.BadRequest, (await b.Api(root + "?" + query)).StatusCode);
    }
    [PostgreSqlFact]
    public async Task ValidationAuthCsrfMassAssignmentAndUtcAreEnforced()
    {
        await using var d = await Database(); using var h = new AuthFactory(d.ConnectionString); using var b = h.Browser();
        foreach (var root in new[] { "tasks", "interviews" }) Assert.Equal(HttpStatusCode.Unauthorized, (await b.Api(root)).StatusCode);
        await Register(b); var a = await Application(b); var input = Input(a.Id, h.Clock.Now.UtcDateTime);
        Assert.Equal(HttpStatusCode.Forbidden, (await b.Api("interviews", HttpMethod.Post, input, csrf: false)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await b.Api("tasks", HttpMethod.Post, new TaskInput("Test"), csrf: false)).StatusCode);
        foreach (var invalid in new[] { input with { Type = "1" }, input with { ScheduledEndUtc = input.ScheduledStartUtc }, input with { MeetingUrl = "javascript:alert(1)" }, input with { Location = new string('x', 301) } }) Assert.Equal(HttpStatusCode.BadRequest, (await b.Api("interviews", HttpMethod.Post, invalid)).StatusCode);
        foreach (var invalid in new[] { new TaskInput(" "), new TaskInput("Test", Priority: "1"), new TaskInput(new string('x', 201)) }) Assert.Equal(HttpStatusCode.BadRequest, (await b.Api("tasks", HttpMethod.Post, invalid)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await b.Api("interviews", HttpMethod.Post, new { applicationId = a.Id, type = "Final", scheduledStartUtc = "2026-10-18T12:00:00", scheduledEndUtc = "2026-10-18T13:00:00" })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await b.Api("tasks", HttpMethod.Post, new { title = "Test", dueAtUtc = "2026-10-18T12:00:00" })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await b.Api("tasks", HttpMethod.Post, new { title = "Test", userId = Guid.NewGuid() })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await b.Api("interviews", HttpMethod.Post, new { applicationId = a.Id, type = "Final", scheduledStartUtc = input.ScheduledStartUtc, scheduledEndUtc = input.ScheduledEndUtc, userId = Guid.NewGuid() })).StatusCode);
        Assert.Empty((await Read<Page<InterviewView>>(await b.Api("interviews?view=all"))).Items); Assert.Empty((await Read<Page<TaskView>>(await b.Api("tasks?view=all"))).Items);
    }
}
