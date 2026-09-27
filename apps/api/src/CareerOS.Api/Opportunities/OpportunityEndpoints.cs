using CareerOS.Application.Opportunities;
using CareerOS.Domain.Common;
using CareerOS.Infrastructure.Opportunities;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace CareerOS.Api.Opportunities;

public static class OpportunityEndpoints
{
    public static void MapOpportunities(this WebApplication app)
    {
        var group = app.MapGroup("/api/v1").RequireAuthorization().WithTags("Companies and jobs");
        group.AddEndpointFilter(async (context, next) =>
        {
            try { return await next(context); }
            catch (DomainValidationException e) { return Results.Problem(statusCode: 400, title: e.Message); }
            catch (OpportunityNotFoundException) { return Results.NotFound(); }
            catch (OpportunityConflictException e) { return Results.Problem(statusCode: 409, title: e.Message); }
            catch (DbUpdateConcurrencyException) { return Results.Problem(statusCode: 409, title: "This record changed. Reload the latest version before editing again."); }
            catch (DbUpdateException e) when (e.InnerException is PostgresException { SqlState: PostgresErrorCodes.ForeignKeyViolation })
            { return Results.Problem(statusCode: 409, title: "This record is referenced by other records, or its company changed. Keep referenced companies; archive jobs with history. Reload before trying again."); }
        });
        group.MapGet("/companies", ([AsParameters] CompanyQuery q, OpportunityService s, CancellationToken ct) => s.Companies(new(q.Page ?? 1, q.PageSize ?? 20, q.Search, q.Location, q.Sort, q.Industry), ct));
        group.MapGet("/companies/{id:guid}", (Guid id, OpportunityService s, CancellationToken ct) => s.GetCompany(id, ct));
        group.MapPost("/companies", async (CompanyInput input, OpportunityService s, CancellationToken ct) =>
        { var saved = await s.SaveCompany(null, input, ct); return Results.Created($"/api/v1/companies/{saved.Id}", saved); });
        group.MapPut("/companies/{id:guid}", (Guid id, CompanyInput input, OpportunityService s, CancellationToken ct) => s.SaveCompany(id, input, ct));
        group.MapDelete("/companies/{id:guid}", async (Guid id, uint version, OpportunityService s, CancellationToken ct) =>
        { await s.DeleteCompany(id, version, ct); return Results.NoContent(); });
        group.MapGet("/jobs", ([AsParameters] JobQuery q, OpportunityService s, CancellationToken ct) => s.Jobs(new(q.Page ?? 1, q.PageSize ?? 20, q.Search, q.Location, q.Sort, CompanyId: q.CompanyId, WorkMode: q.WorkMode, EmploymentType: q.EmploymentType, SeniorityLevel: q.SeniorityLevel, Source: q.Source, State: q.State), ct));
        group.MapGet("/jobs/{id:guid}", (Guid id, OpportunityService s, CancellationToken ct) => s.GetJob(id, ct));
        group.MapPost("/jobs", async (JobInput input, OpportunityService s, CancellationToken ct) =>
        { var saved = await s.SaveJob(null, input, ct); return Results.Created($"/api/v1/jobs/{saved.Id}", saved); });
        group.MapPut("/jobs/{id:guid}", (Guid id, JobInput input, OpportunityService s, CancellationToken ct) => s.SaveJob(id, input, ct));
        group.MapPut("/jobs/{id:guid}/archive", (Guid id, ArchiveInput input, OpportunityService s, CancellationToken ct) => s.Archive(id, input, ct));
        group.MapDelete("/jobs/{id:guid}", async (Guid id, uint version, OpportunityService s, CancellationToken ct) =>
        { await s.DeleteJob(id, version, ct); return Results.NoContent(); });
    }
}
public sealed record CompanyQuery(int? Page, int? PageSize, string? Search, string? Location, string? Sort, string? Industry);
public sealed record JobQuery(int? Page, int? PageSize, string? Search, string? Location, string? Sort, Guid? CompanyId,
    string? WorkMode, string? EmploymentType, string? SeniorityLevel, string? Source, string? State);
