using CareerOS.Application.Applications;
using CareerOS.Domain.Common;
using CareerOS.Infrastructure.Applications;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace CareerOS.Api.Applications;

public static class ApplicationEndpoints
{
    public static void MapApplications(this WebApplication app)
    {
        var g = app.MapGroup("/api/v1/applications").RequireAuthorization().WithTags("Applications");
        g.AddEndpointFilter(async (context, next) =>
        {
            try { return await next(context); }
            catch (DomainValidationException e) { return Results.Problem(statusCode: 400, title: e.Message); }
            catch (ApplicationNotFoundException) { return Results.NotFound(); }
            catch (ApplicationConflictException e) { return Results.Problem(statusCode: 409, title: e.Message, extensions: new Dictionary<string, object?> { ["existingApplicationId"] = e.ExistingId }); }
            catch (DbUpdateConcurrencyException) { return Results.Problem(statusCode: 409, title: "This record changed. Reload the latest version before editing again."); }
            catch (DbUpdateException e) when (e.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
            { return Results.Problem(statusCode: 409, title: "An application already exists or its history changed. Reload before trying again."); }
            catch (DbUpdateException e) when (e.InnerException is PostgresException { SqlState: PostgresErrorCodes.ForeignKeyViolation })
            { return Results.Problem(statusCode: 409, title: "A related record changed. Reload before trying again."); }
        });
        g.MapGet("", ([AsParameters] Query q, ApplicationService s, CancellationToken ct) => s.List(q.Input(), ct));
        g.MapGet("/summary", ([AsParameters] Query q, ApplicationService s, CancellationToken ct) => s.Summary(q.Input(), ct));
        g.MapGet("/resume-options", (string? search, ApplicationService s, CancellationToken ct) => s.Resumes(search, ct));
        g.MapGet("/{id:guid}", (Guid id, ApplicationService s, CancellationToken ct) => s.Get(id, ct));
        g.MapPost("", async (CreateApplicationInput input, ApplicationService s, CancellationToken ct) =>
        { var a = await s.Create(input, ct); return Results.Created($"/api/v1/applications/{a.Id}", a); });
        g.MapPut("/{id:guid}", (Guid id, EditApplicationInput input, ApplicationService s, CancellationToken ct) => s.Edit(id, input, ct));
        g.MapDelete("/{id:guid}", async (Guid id, ApplicationService s, CancellationToken ct) => { await s.Delete(id, ct); return Results.NoContent(); });
        g.MapPost("/{id:guid}/status", (Guid id, StatusInput input, ApplicationService s, CancellationToken ct) => s.Transition(id, input, ct));
        g.MapPost("/{id:guid}/archive", (Guid id, VersionInput input, ApplicationService s, CancellationToken ct) => s.Archive(id, input, false, ct));
        g.MapPost("/{id:guid}/restore", (Guid id, VersionInput input, ApplicationService s, CancellationToken ct) => s.Archive(id, input, true, ct));
        g.MapGet("/{id:guid}/history", (Guid id, int? page, ApplicationService s, CancellationToken ct) => s.History(id, page ?? 1, ct));
        g.MapGet("/{id:guid}/notes", (Guid id, int? page, ApplicationService s, CancellationToken ct) => s.Notes(id, page ?? 1, ct));
        g.MapPost("/{id:guid}/notes", async (Guid id, NoteInput input, ApplicationService s, CancellationToken ct) =>
        { var note = await s.SaveNote(id, null, input, ct); return Results.Created($"/api/v1/applications/{id}/notes/{note.Id}", note); });
        g.MapPut("/{id:guid}/notes/{noteId:guid}", (Guid id, Guid noteId, NoteInput input, ApplicationService s, CancellationToken ct) => s.SaveNote(id, noteId, input, ct));
        g.MapDelete("/{id:guid}/notes/{noteId:guid}", async (Guid id, Guid noteId, uint version, ApplicationService s, CancellationToken ct) =>
        { await s.DeleteNote(id, noteId, version, ct); return Results.NoContent(); });
    }
    public sealed record Query(int? Page, int? PageSize, string? Search, string? Status, Guid? CompanyId, Guid? JobId, string? State, string? Sort, DateTime? AppliedFrom, DateTime? AppliedTo)
    { public ApplicationQuery Input() => new(Page ?? 1, PageSize ?? 20, Search, Status, CompanyId, JobId, State, Sort, AppliedFrom, AppliedTo); }
}
