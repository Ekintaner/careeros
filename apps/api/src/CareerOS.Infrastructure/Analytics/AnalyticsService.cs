using System.Data;
using CareerOS.Application.Analytics;
using CareerOS.Application.Authentication;
using CareerOS.Domain.Applications;
using CareerOS.Domain.Opportunities;
using CareerOS.Domain.Planning;
using CareerOS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
namespace CareerOS.Infrastructure.Analytics;
public sealed class AnalyticsService(CareerOSDbContext db, OwnedData owned, ICurrentUser current, TimeProvider clock)
{
    public async Task<AnalyticsView> Get(AnalyticsQuery q, CancellationToken ct)
    {
        var (start, end, zone) = q.Bounds(clock); var now = clock.GetUtcNow().UtcDateTime;
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.RepeatableRead, ct);
        var apps = owned.Query<JobApplication>().Where(x => x.CreatedAtUtc >= start && x.CreatedAtUtc < end);
        var history = owned.Query<ApplicationStatusHistory>();
        var interviews = owned.Query<Interview>();
        var summary = await apps.GroupBy(x => 1).Select(g => new
        {
            Total = g.Count(),
            Applied = g.Count(x => x.AppliedAtUtc != null),
            Interviewed = g.Count(x => x.AppliedAtUtc != null && (history.Any(h => h.ApplicationId == x.Id && h.ToStatus == ApplicationStatus.Interview) || interviews.Any(i => i.ApplicationId == x.Id))),
            Offered = g.Count(x => x.AppliedAtUtc != null && history.Any(h => h.ApplicationId == x.Id && h.ToStatus == ApplicationStatus.Offer))
        }).SingleOrDefaultAsync(ct);
        var stages = await apps.GroupBy(x => x.Status).Select(g => new MetricRow(g.Key.ToString(), g.Count())).ToListAsync(ct);
        // Only daily aggregates leave PostgreSQL; the validated range bounds this to 733 rows.
        var days = await apps.GroupBy(x => TimeZoneInfo.ConvertTimeBySystemTimeZoneId(x.CreatedAtUtc, q.TimeZone).Date)
            .Select(g => new { Day = g.Key, Count = g.Count() }).ToListAsync(ct);
        DateTime Bucket(DateTime d) => q.Interval == "month" ? new(d.Year, d.Month, 1) : d.AddDays(-((int)d.DayOfWeek + 6) % 7);
        var counts = days.GroupBy(x => Bucket(x.Day)).ToDictionary(g => g.Key, g => g.Sum(x => x.Count));
        var trends = new List<MetricRow>();
        for (var d = Bucket(TimeZoneInfo.ConvertTimeFromUtc(start, zone).Date); d < TimeZoneInfo.ConvertTimeFromUtc(end, zone).Date; d = q.Interval == "month" ? d.AddMonths(1) : d.AddDays(7))
            trends.Add(new(d.ToString("yyyy-MM-dd"), counts.GetValueOrDefault(d)));
        var joined = from a in apps join j in owned.Query<Job>() on a.JobId equals j.Id join c in owned.Query<Company>() on j.CompanyId equals c.Id select new { c.Id, c.Name, j.Title };
        var companies = await joined.GroupBy(x => new { x.Id, x.Name }).OrderByDescending(g => g.Count()).ThenBy(g => g.Key.Name).Take(20).Select(g => new MetricRow(g.Key.Name, g.Count())).ToListAsync(ct);
        var roles = await joined.GroupBy(x => x.Title).OrderByDescending(g => g.Count()).ThenBy(g => g.Key).Take(20).Select(g => new MetricRow(g.Key, g.Count())).ToListAsync(ct);
        var owner = current.UserId;
        var durations = await db.Database.SqlQuery<DurationRow>($"""
            WITH events AS (
              SELECT h."ToStatus", h."Sequence", h."ChangedAtUtc",
                lead(h."ChangedAtUtc") OVER w AS next_at, lead(h."Sequence") OVER w AS next_seq
              FROM application_status_history h JOIN job_applications a ON a."Id" = h."ApplicationId" AND a."UserId" = h."UserId"
              WHERE h."UserId" = {owner} AND a."CreatedAtUtc" >= {start} AND a."CreatedAtUtc" < {end}
              WINDOW w AS (PARTITION BY h."ApplicationId" ORDER BY h."Sequence")
            ) SELECT "ToStatus" AS "Label", count(*)::int AS "Count",
              avg(extract(epoch from (next_at - "ChangedAtUtc")) / 86400)::double precision AS "AverageDays"
              FROM events WHERE next_seq = "Sequence" + 1 AND next_at >= "ChangedAtUtc"
              GROUP BY "ToStatus"
            """).ToListAsync(ct);
        var ip = interviews.Where(x => x.ScheduledStartUtc >= start && x.ScheduledStartUtc < end);
        var istats = await ip.GroupBy(x => x.ScheduledEndUtc > now ? "Upcoming/in progress" : "Past").Select(g => new MetricRow(g.Key, g.Count())).ToListAsync(ct);
        var tasks = owned.Query<CareerTask>().Where(x => x.CreatedAtUtc >= start && x.CreatedAtUtc < end);
        var tstats = await tasks.GroupBy(x => x.CompletedAtUtc != null ? "Completed" : x.DueAtUtc < now ? "Overdue" : "Open").Select(g => new MetricRow(g.Key, g.Count())).ToListAsync(ct);
        await tx.CommitAsync(ct);
        return new(start, end, q.TimeZone, now, summary?.Total ?? 0, summary?.Applied ?? 0, summary?.Interviewed ?? 0, summary?.Offered ?? 0, stages, trends, companies, roles, durations, istats, tstats);
    }
}
