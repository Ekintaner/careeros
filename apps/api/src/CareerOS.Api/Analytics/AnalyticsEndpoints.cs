using CareerOS.Application.Analytics;
using CareerOS.Domain.Common;
using CareerOS.Infrastructure.Analytics;
namespace CareerOS.Api.Analytics;
public static class AnalyticsEndpoints
{
    public static void MapAnalytics(this WebApplication app) => app.MapGet("/api/v1/analytics", async (DateOnly? from, DateOnly? to, string? timeZone, string? interval, AnalyticsService service, CancellationToken ct) =>
    {
        try { return Results.Ok(await service.Get(new AnalyticsQuery(from, to, timeZone ?? "UTC", interval ?? "week"), ct)); }
        catch (DomainValidationException e) { return Results.Problem(statusCode: 400, title: e.Message); }
    }).RequireAuthorization().WithTags("Analytics");
}
