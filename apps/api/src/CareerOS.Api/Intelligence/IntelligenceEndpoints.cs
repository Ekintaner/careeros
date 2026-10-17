using CareerOS.Application.Intelligence;
using CareerOS.Domain.Common;
using CareerOS.Infrastructure.Intelligence;

namespace CareerOS.Api.Intelligence;
public static class IntelligenceEndpoints
{
    public static void MapIntelligence(this WebApplication app)
    {
        var group = app.MapGroup("/api/v1/intelligence/skills").RequireAuthorization().WithTags("Skills intelligence");
        group.AddEndpointFilter(async (context, next) =>
        {
            try { return await next(context); }
            catch (DomainValidationException e) { return Results.Problem(statusCode: 400, title: e.Message); }
            catch (IntelligenceNotFoundException) { return Results.NotFound(); }
        });
        group.MapGet("", ([AsParameters] IntelligenceParameters q, IntelligenceService s, CancellationToken ct) => s.Get(q.Query(), ct));
        group.MapGet("/{id:guid}/jobs", (Guid id, [AsParameters] IntelligenceParameters q, IntelligenceService s, CancellationToken ct) => s.SkillJobs(id, q.Query(), ct));
    }
}
public sealed record IntelligenceParameters(int? Page, int? PageSize, string? State, string? Status, string? Sort, string? Search)
{
    public IntelligenceQuery Query() => new(Page ?? 1, PageSize ?? 20, State ?? "active", Status ?? "all", Sort ?? "priority", Search);
}
