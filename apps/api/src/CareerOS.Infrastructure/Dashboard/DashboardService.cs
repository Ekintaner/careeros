using System.Data;
using CareerOS.Application.Dashboard;
using CareerOS.Domain.Applications;
using CareerOS.Domain.Opportunities;
using CareerOS.Domain.Planning;
using CareerOS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
namespace CareerOS.Infrastructure.Dashboard;
public sealed class DashboardService(CareerOSDbContext db, OwnedData owned, TimeProvider clock)
{
    public async Task<DashboardView> Get(CancellationToken ct)
    {
        var now = clock.GetUtcNow().UtcDateTime;
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.RepeatableRead, ct);
        var applications = owned.Query<JobApplication>(); var jobs = owned.Query<Job>(); var companies = owned.Query<Company>();
        var summary = await applications.GroupBy(x => 1).Select(g => new { Total = g.Count(), Active = g.Count(a => a.Status == ApplicationStatus.Saved || a.Status == ApplicationStatus.Applied || a.Status == ApplicationStatus.Assessment || a.Status == ApplicationStatus.Interview || a.Status == ApplicationStatus.Offer), Offers = g.Count(a => a.Status == ApplicationStatus.Offer) }).SingleOrDefaultAsync(ct);
        var jobCount = await jobs.CountAsync(x => x.ArchivedAtUtc == null, ct);
        var tasks = owned.Query<CareerTask>().Where(x => x.CompletedAtUtc == null);
        var taskStats = await tasks.GroupBy(x => 1).Select(g => new { Total = g.Count(), Overdue = g.Count(x => x.DueAtUtc < now) }).SingleOrDefaultAsync(ct);
        var interviews = owned.Query<Interview>().Where(x => x.ScheduledEndUtc > now);
        var interviewCount = await interviews.CountAsync(ct);
        var recent = await (from a in applications join j in jobs on a.JobId equals j.Id join c in companies on j.CompanyId equals c.Id orderby a.UpdatedAtUtc descending, a.Id select new DashboardApplication(a.Id, j.Title, c.Name, a.Status.ToString())).Take(5).ToListAsync(ct);
        var next = await (from i in interviews join a in applications on i.ApplicationId equals a.Id join j in jobs on a.JobId equals j.Id join c in companies on j.CompanyId equals c.Id orderby i.ScheduledStartUtc, i.Id select new DashboardInterview(i.Id, j.Title, c.Name, i.ScheduledStartUtc, i.ScheduledEndUtc)).Take(5).ToListAsync(ct);
        var nextTasks = await tasks.OrderBy(x => x.DueAtUtc == null).ThenBy(x => x.DueAtUtc).ThenByDescending(x => x.Priority == TaskPriority.High ? 2 : x.Priority == TaskPriority.Normal ? 1 : 0).ThenBy(x => x.Id).Take(5).Select(x => new DashboardTask(x.Id, x.Title, x.DueAtUtc, x.Priority.ToString())).ToListAsync(ct);
        await tx.CommitAsync(ct);
        return new(now, summary?.Total ?? 0, summary?.Active ?? 0, summary?.Offers ?? 0, jobCount, taskStats?.Total ?? 0, taskStats?.Overdue ?? 0, interviewCount, recent, next, nextTasks);
    }
}
