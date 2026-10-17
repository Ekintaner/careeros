using System.Data.Common;
using System.Diagnostics;
using System.Text.Json;
using CareerOS.Application.Authentication;
using CareerOS.Application.Analytics;
using CareerOS.Application.Intelligence;
using CareerOS.Application.Planning;
using CareerOS.Application.Matching;
using CareerOS.Infrastructure.Matching;
using CareerOS.Domain.Accounts;
using CareerOS.Domain.Applications;
using CareerOS.Domain.Opportunities;
using CareerOS.Domain.Planning;
using CareerOS.Domain.Skills;
using CareerOS.Infrastructure.Analytics;
using CareerOS.Infrastructure.Dashboard;
using CareerOS.Infrastructure.Intelligence;
using CareerOS.Infrastructure.Persistence;
using CareerOS.Infrastructure.Planning;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Xunit;
using Xunit.Abstractions;

namespace CareerOS.Tests.Integration;

[Collection("PostgreSQL")]
public sealed class HardeningPerformanceTests(PostgreSqlFixture fixture, ITestOutputHelper output)
{
    private sealed record Owner(Guid UserId) : ICurrentUser;
    private sealed class Commands : DbCommandInterceptor
    {
        public int Count { get; set; }
        public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(DbCommand command, CommandEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
        { Count++; return ValueTask.FromResult(result); }
        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
        { Count++; return ValueTask.FromResult(result); }
    }

    [PostgreSqlFact]
    public async Task SkillReplacementHasBoundedRoundTrips()
    {
        await using var database = await fixture.CreateDatabaseAsync();
        await using (var schema = database.Context()) await schema.Database.MigrateAsync();
        var commands = new Commands(); var clock = new TestClock();
        await using var db = new CareerOSDbContext(new DbContextOptionsBuilder<CareerOSDbContext>().UseNpgsql(database.ConnectionString).AddInterceptors(commands).Options, clock);
        var user = new User("synthetic-write@example.com", "Synthetic"); var company = new Company(user.Id, "Synthetic"); var job = new Job(company, "Synthetic");
        db.AddRange(user, company, job); await db.SaveChangesAsync();
        var current = new Owner(user.Id); var service = new MatchingService(db, new OwnedData(db, current), clock, current);
        var results = new List<object>();
        foreach (var size in new[] { 10, 100 })
        {
            var version = db.Entry(job).Property<uint>("Version").CurrentValue;
            commands.Count = 0; var watch = Stopwatch.StartNew();
            await service.SaveJob(job.Id, new JobSkillsInput(version, Enumerable.Range(0, size).Select(i => new RequirementInput($"Batch skill {i:D3}", "Required")).ToArray()), default);
            watch.Stop();
            results.Add(new { skills = size, commands = commands.Count, elapsedMs = watch.Elapsed.TotalMilliseconds });
            Assert.InRange(commands.Count, 1, 12);
            Assert.Equal(size, await db.JobSkills.CountAsync());
        }
        var json = JsonSerializer.Serialize(results, new JsonSerializerOptions { WriteIndented = true }); output.WriteLine(json);
        var destination = Environment.GetEnvironmentVariable("CAREEROS_WRITE_PERF_OUTPUT");
        if (!string.IsNullOrWhiteSpace(destination)) await File.WriteAllTextAsync(destination, json);
    }

    [PostgreSqlFact]
    public async Task RepresentativeWorkloadsKeepQueryAndResponseBudgets()
    {
        await using var database = await fixture.CreateDatabaseAsync();
        var clock = new TestClock();
        var user = new User("synthetic-perf@example.com", "CODEX-014 synthetic");
        var other = new User("synthetic-other@example.com", "Other synthetic owner");
        var skills = Enumerable.Range(0, 20).Select(i => new Skill($"Synthetic skill {i:D2}")).ToArray();
        await using (var seed = database.Context(clock))
        {
            await seed.Database.MigrateAsync();
            seed.AddRange(user, other); seed.AddRange(skills);
            seed.AddRange(skills.Take(10).Select(s => new UserSkill(user.Id, s.Id)));
            await seed.SaveChangesAsync();
        }
        var samples = new List<object>();
        var previous = 0;
        var sizes = Environment.GetEnvironmentVariable("CAREEROS_PERF_LARGE") == "true" ? new[] { 100, 1000, 5000 } : new[] { 100, 1000 };
        foreach (var size in sizes)
        {
            // Identical cardinality for a second owner catches accidental global aggregates.
            await using (var seed = database.Context(clock))
            {
                foreach (var owner in new[] { user, other })
                {
                    for (var i = previous; i < size; i++)
                    {
                        var company = new Company(owner.Id, $"Synthetic company {i % 25:D2}");
                        var job = new Job(company, $"Synthetic role {i % 10:D2}");
                        var app = new JobApplication(job, clock, i % 3 == 0 ? ApplicationStatus.Saved : ApplicationStatus.Applied);
                        var task = new CareerTask(owner.Id, $"Synthetic task {i:D5}");
                        task.LinkApplication(app); task.Plan(null, i % 5 == 0 ? null : clock.Now.UtcDateTime.AddDays(i % 15 - 7), (TaskPriority)(i % 3));
                        if (i % 4 == 0) task.Complete(clock);
                        seed.AddRange(company, job, app, task);
                        seed.Add(new Interview(app, InterviewType.Technical, clock.Now.UtcDateTime.AddDays(i % 30 - 10), clock.Now.UtcDateTime.AddDays(i % 30 - 10).AddHours(1)));
                        seed.AddRange(Enumerable.Range(0, 5).Select(k => new JobSkill(job, skills[(i + k) % skills.Length].Id, k % 2 == 0 ? RequirementLevel.Required : RequirementLevel.Preferred)));
                        if ((i + 1) % 100 == 0) { await seed.SaveChangesAsync(); seed.ChangeTracker.Clear(); }
                    }
                    await seed.SaveChangesAsync(); seed.ChangeTracker.Clear();
                }
            }
            previous = size;
            await database.ExecuteAsync("ANALYZE");
            var commands = new Commands();
            await using var db = new CareerOSDbContext(new DbContextOptionsBuilder<CareerOSDbContext>().UseNpgsql(database.ConnectionString).AddInterceptors(commands).Options, clock);
            var current = new Owner(user.Id); var owned = new OwnedData(db, current);
            var dashboard = new DashboardService(db, owned, clock);
            var analytics = new AnalyticsService(db, owned, current, clock);
            var intelligence = new IntelligenceService(db, owned);
            var planning = new PlanningService(db, owned, current, clock);
            async Task Measure(string name, int budget, Func<Task> run)
            {
                await run(); // Warm EF translation/JIT and database buffers, excluded from samples.
                var ms = new List<double>(); var counts = new List<int>();
                for (var i = 0; i < 10; i++)
                {
                    commands.Count = 0; var watch = Stopwatch.StartNew();
                    await run(); watch.Stop();
                    ms.Add(watch.Elapsed.TotalMilliseconds); counts.Add(commands.Count);
                    Assert.Equal(budget, commands.Count);
                    Assert.Empty(db.ChangeTracker.Entries());
                }
                var sorted = ms.Order().ToArray();
                samples.Add(new { workload = name, recordsPerOwner = size, owners = 2, samplesMs = ms, queryCounts = counts, medianMs = (sorted[4] + sorted[5]) / 2, p95NearestRankMs = sorted[9] });
            }
            await Measure("dashboard", 7, async () => { var v = await dashboard.Get(default); Assert.Equal(size, v.TotalApplications); Assert.Equal(5, v.RecentApplications.Count); Assert.InRange(v.NextTasks.Count, 0, 5); });
            await Measure("analytics", 8, async () => { var v = await analytics.Get(new AnalyticsQuery(), default); Assert.Equal(size, v.Total); Assert.InRange(v.Companies.Count, 0, 20); });
            await Measure("skills-intelligence", 4, async () => { var v = await intelligence.Get(new IntelligenceQuery(), default); Assert.Equal(size, v.Summary.TotalJobs); Assert.Equal(20, v.Items.Count); });
            await Measure("tasks-first-page", 2, async () => { var v = await planning.Tasks(new PlanningQuery(), default); Assert.Equal(20, v.Items.Count); });
        }
        var json = JsonSerializer.Serialize(new { measuredAtUtc = DateTimeOffset.UtcNow, scope = "In-process service + real PostgreSQL; sequential warm requests; no HTTP/rendering/auth; 10 samples, no latency pass threshold", samples }, new JsonSerializerOptions { WriteIndented = true });
        output.WriteLine(json);
        var destination = Environment.GetEnvironmentVariable("CAREEROS_PERF_OUTPUT");
        if (!string.IsNullOrWhiteSpace(destination)) await File.WriteAllTextAsync(destination, json);
    }
}
