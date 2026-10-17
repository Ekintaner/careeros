using CareerOS.Application.Matching;
using CareerOS.Domain.Common;
using CareerOS.Infrastructure.Matching;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace CareerOS.Api.Matching;

public static class MatchingEndpoints
{
    public static void MapMatching(this WebApplication app)
    {
        var group = app.MapGroup("/api/v1").RequireAuthorization().WithTags("Skill matching");
        group.AddEndpointFilter(async (context, next) =>
        {
            try { return await next(context); }
            catch (DomainValidationException e) { return Results.Problem(statusCode: 400, title: e.Message); }
            catch (MatchingNotFoundException) { return Results.NotFound(); }
            catch (MatchingConflictException) { return Conflict(); }
            catch (DbUpdateConcurrencyException) { return Conflict(); }
            catch (DbUpdateException e) when (e.InnerException is PostgresException { SqlState: PostgresErrorCodes.ForeignKeyViolation })
            { return Conflict(); }
        });
        group.MapGet("/jobs/{id:guid}/matching", (Guid id, Guid? resumeId, MatchingService s, CancellationToken ct) => s.Get(id, resumeId, ct));
        group.MapGet("/resumes/{id:guid}/skills", (Guid id, MatchingService s, CancellationToken ct) => s.Resume(id, ct));
        group.MapPut("/jobs/{id:guid}/skills", async (Guid id, JobSkillsInput input, MatchingService s, CancellationToken ct) =>
        { await s.SaveJob(id, input, ct); return Results.NoContent(); });
        group.MapPut("/resumes/{id:guid}/skills", async (Guid id, ResumeSkillsInput input, MatchingService s, CancellationToken ct) =>
        { await s.SaveResume(id, input, ct); return Results.NoContent(); });
    }
    private static IResult Conflict() => Results.Problem(statusCode: 409, title: "This record changed. Reload the latest skills before saving again.");
}
