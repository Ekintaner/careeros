using CareerOS.Domain.Common;
namespace CareerOS.Application.Analytics;
public sealed record AnalyticsQuery(DateOnly? From = null, DateOnly? To = null, string TimeZone = "UTC", string Interval = "week")
{
    public (DateTime Start, DateTime End, TimeZoneInfo Zone) Bounds(TimeProvider clock)
    {
        if (TimeZone.Length > 100 || (TimeZone != "UTC" && !TimeZoneInfo.TryConvertIanaIdToWindowsId(TimeZone, out _))) throw new DomainValidationException("Use an IANA time zone such as Europe/Kyiv or UTC.");
        TimeZoneInfo zone;
        try { zone = TimeZoneInfo.FindSystemTimeZoneById(TimeZone); }
        catch (Exception e) when (e is TimeZoneNotFoundException or InvalidTimeZoneException or ArgumentException) { throw new DomainValidationException("Choose a valid time zone."); }
        var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(clock.GetUtcNow(), zone).DateTime);
        var end = To ?? today;
        if (end.Year < 1900 || end.Year >= 9999) throw new DomainValidationException("Choose dates from 1900 through 9998.");
        var start = From ?? end.AddDays(-89);
        if (Interval is not ("week" or "month") || start > end || end.DayNumber - start.DayNumber > 731 || start.Year < 1900 || end.Year >= 9999)
            throw new DomainValidationException("Choose an ordered date range of at most 732 days and week or month intervals.");
        DateTime Utc(DateOnly d) {
            var local = d.ToDateTime(TimeOnly.MinValue);
            if (zone.IsInvalidTime(local) || zone.IsAmbiguousTime(local)) throw new DomainValidationException("A date boundary is ambiguous or missing in this time zone. Choose another boundary.");
            return TimeZoneInfo.ConvertTimeToUtc(local, zone);
        }
        return (Utc(start), Utc(end.AddDays(1)), zone);
    }
}
public sealed record MetricRow(string Label, int Count);
public sealed record DurationRow(string Label, int Count, double AverageDays);
public sealed record AnalyticsView(DateTime StartUtc, DateTime EndExclusiveUtc, string TimeZone, DateTime AsOfUtc,
    int Total, int Applied, int Interviewed, int Offered, IReadOnlyList<MetricRow> Stages,
    IReadOnlyList<MetricRow> Trends, IReadOnlyList<MetricRow> Companies, IReadOnlyList<MetricRow> Roles,
    IReadOnlyList<DurationRow> Durations, IReadOnlyList<MetricRow> Interviews, IReadOnlyList<MetricRow> Tasks);
