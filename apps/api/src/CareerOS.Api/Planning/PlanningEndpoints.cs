using CareerOS.Application.Applications;
using CareerOS.Application.Planning;
using CareerOS.Domain.Common;
using CareerOS.Infrastructure.Planning;
using Microsoft.EntityFrameworkCore;
using Npgsql;
namespace CareerOS.Api.Planning;

public static class PlanningEndpoints
{
    public static void MapPlanning(this WebApplication app)
    {
        var g = app.MapGroup("/api/v1").RequireAuthorization().WithTags("Planning");
        g.AddEndpointFilter(async (context, next) =>
        {
            try { return await next(context); }
            catch (DomainValidationException e) { return Results.Problem(statusCode: 400, title: e.Message); }
            catch (ApplicationNotFoundException) { return Results.NotFound(); }
            catch (ApplicationConflictException e) { return Results.Problem(statusCode: 409, title: e.Message); }
            catch (DbUpdateConcurrencyException) { return Results.Problem(statusCode: 409, title: "This record changed. Reload before editing again."); }
            catch (DbUpdateException e) when (e.InnerException is PostgresException { SqlState: PostgresErrorCodes.ForeignKeyViolation })
            { return Results.Problem(statusCode: 409, title: "A related record exists or changed. Reload before trying again."); }
        });
        g.MapGet("/interviews", ([AsParameters] Query q, PlanningService s, CancellationToken ct) => s.Interviews(q.Input(), ct));
        g.MapGet("/interviews/{id:guid}", (Guid id, PlanningService s, CancellationToken ct) => s.Interview(id, ct));
        g.MapPost("/interviews", async (InterviewInput input, PlanningService s, CancellationToken ct) => { var x = await s.SaveInterview(null, input, ct); return Results.Created($"/api/v1/interviews/{x.Id}", x); });
        g.MapPut("/interviews/{id:guid}", (Guid id, InterviewInput input, PlanningService s, CancellationToken ct) => s.SaveInterview(id, input, ct));
        g.MapDelete("/interviews/{id:guid}", async (Guid id, uint version, PlanningService s, CancellationToken ct) => { await s.DeleteInterview(id, version, ct); return Results.NoContent(); });
        g.MapGet("/interviews/{id:guid}/notes", (Guid id, int? page, PlanningService s, CancellationToken ct) => s.Notes(id, page ?? 1, ct));
        g.MapPost("/interviews/{id:guid}/notes", async (Guid id, NoteInput input, PlanningService s, CancellationToken ct) => { var x = await s.SaveNote(id, null, input, ct); return Results.Created($"/api/v1/interviews/{id}/notes/{x.Id}", x); });
        g.MapPut("/interviews/{id:guid}/notes/{noteId:guid}", (Guid id, Guid noteId, NoteInput input, PlanningService s, CancellationToken ct) => s.SaveNote(id, noteId, input, ct));
        g.MapDelete("/interviews/{id:guid}/notes/{noteId:guid}", async (Guid id, Guid noteId, uint version, PlanningService s, CancellationToken ct) => { await s.DeleteNote(id, noteId, version, ct); return Results.NoContent(); });
        g.MapGet("/tasks", ([AsParameters] Query q, PlanningService s, CancellationToken ct) => s.Tasks(q.Input(), ct));
        g.MapGet("/tasks/{id:guid}", (Guid id, PlanningService s, CancellationToken ct) => s.Task(id, ct));
        g.MapPost("/tasks", async (TaskInput input, PlanningService s, CancellationToken ct) => { var x = await s.SaveTask(null, input, ct); return Results.Created($"/api/v1/tasks/{x.Id}", x); });
        g.MapPut("/tasks/{id:guid}", (Guid id, TaskInput input, PlanningService s, CancellationToken ct) => s.SaveTask(id, input, ct));
        g.MapDelete("/tasks/{id:guid}", async (Guid id, uint version, PlanningService s, CancellationToken ct) => { await s.DeleteTask(id, version, ct); return Results.NoContent(); });
    }
    public sealed record Query(int? Page, int? PageSize, Guid? ApplicationId, string? View)
    { public PlanningQuery Input() => new(Page ?? 1, PageSize ?? 20, ApplicationId, View); }
}
