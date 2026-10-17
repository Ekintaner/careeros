using CareerOS.Domain.Common;
using CareerOS.Infrastructure.Documents;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace CareerOS.Api.Documents;

public static class DocumentEndpoints
{
    public static void MapDocuments(this WebApplication app)
    {
        var g = app.MapGroup("/api/v1/documents").RequireAuthorization().WithTags("Documents");
        g.AddEndpointFilter(async (context, next) =>
        {
            context.HttpContext.Response.Headers.CacheControl = "no-store";
            try { return await next(context); }
            catch (DomainValidationException e) { return Results.Problem(statusCode: 400, title: e.Message); }
            catch (DocumentTooLargeException) { return Results.Problem(statusCode: 413, title: "Files must be at most 5 MiB."); }
            catch (DocumentMissingException) { return Results.NotFound(); }
            catch (DocumentConflictException e) { return Results.Problem(statusCode: 409, title: e.Message); }
            catch (DbUpdateConcurrencyException) { return Results.Conflict(); }
            catch (DbUpdateException e) when (e.InnerException is PostgresException { SqlState: PostgresErrorCodes.ForeignKeyViolation or PostgresErrorCodes.UniqueViolation })
            { return Results.Problem(statusCode: 409, title: "A related record changed. Reload and try again."); }
        });
        g.MapGet("", (int? page, string? type, DocumentService s, CancellationToken ct) => s.List(page ?? 1, type, ct));
        // Raw bounded binary body avoids multipart temporary files and framework filename handling.
        g.MapPost("", async (HttpRequest request, string filename, string name, string type, DocumentService s, CancellationToken ct) =>
        {
            if (request.ContentLength > LocalDocumentStore.MaxBytes) throw new DocumentTooLargeException();
            var d = await s.Upload(request.Body, request.ContentType ?? "", filename, name, type, ct);
            return Results.Created($"/api/v1/documents/{d.Id}", d);
        });
        g.MapGet("/{id:guid}/download", async (Guid id, DocumentService s, CancellationToken ct) =>
        {
            var file = await s.Download(id, ct);
            return Results.File(file.Stream, "application/pdf", file.Name, enableRangeProcessing: false);
        });
        g.MapDelete("/{id:guid}", async (Guid id, DocumentService s, CancellationToken ct) => { await s.Delete(id, ct); return Results.NoContent(); });
        g.MapPost("/resumes/{id:guid}/primary", async (Guid id, DocumentService s, CancellationToken ct) => { await s.Primary(id, ct); return Results.NoContent(); });
    }
}
