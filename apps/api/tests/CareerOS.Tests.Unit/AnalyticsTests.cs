using CareerOS.Application.Analytics;
using CareerOS.Domain.Common;
using Xunit;
namespace CareerOS.Tests.Unit;
public class AnalyticsTests
{
    [Fact] public void CalendarBoundsRespectDstAndInclusiveEnd() { var b = new AnalyticsQuery(new(2026, 3, 8), new(2026, 3, 8), "America/New_York").Bounds(TimeProvider.System); Assert.Equal(23, (b.End - b.Start).TotalHours); Assert.Equal(DateTimeKind.Utc, b.Start.Kind); }
    [Theory][InlineData("bad", "week")][InlineData("UTC", "day")] public void InvalidZoneOrIntervalRejected(string z, string i) => Assert.Throws<DomainValidationException>(() => new AnalyticsQuery(TimeZone: z, Interval: i).Bounds(TimeProvider.System));
    [Fact] public void ReversedAndUnboundedRangesRejected() { Assert.Throws<DomainValidationException>(() => new AnalyticsQuery(new(2026, 2, 2), new(2026, 1, 1)).Bounds(TimeProvider.System)); Assert.Throws<DomainValidationException>(() => new AnalyticsQuery(new(2020, 1, 1), new(2026, 1, 1)).Bounds(TimeProvider.System)); }
}
