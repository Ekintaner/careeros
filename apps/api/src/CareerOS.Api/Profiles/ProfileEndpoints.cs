using CareerOS.Application.Profiles;
using CareerOS.Domain.Common;
using CareerOS.Infrastructure.Profiles;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace CareerOS.Api.Profiles;

public static class ProfileEndpoints
{
    public static void MapCareerProfile(this WebApplication app)
    {
        var group = app.MapGroup("/api/v1").RequireAuthorization().WithTags("Profile and settings");
        group.AddEndpointFilter(async (context, next) =>
        {
            try { return await next(context); }
            catch (DomainValidationException e) { return Results.Problem(statusCode: 400, title: e.Message); }
            catch (ResourceNotFoundException) { return Results.NotFound(); }
            catch (EditConflictException) { return Conflict(); }
            catch (DbUpdateConcurrencyException) { return Conflict(); }
            catch (DbUpdateException e) when (e.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation }) { return Conflict(); }
        });
        group.MapGet("/profile", (ProfileService service, CancellationToken ct) => service.Get(ct));
        group.MapPut("/profile", (ProfileInput input, ProfileService service, CancellationToken ct) => service.Update(input, ct));
        group.MapGet("/profile/skills", (ProfileService service, CancellationToken ct) => service.Skills(ct));
        group.MapPost("/profile/skills", async (AddSkillInput input, ProfileService service, CancellationToken ct) =>
        {
            var skill = await service.AddSkill(input, ct);
            return Results.Created($"/api/v1/profile/skills/{skill.SkillId}", skill);
        });
        group.MapPut("/profile/skills/{skillId:guid}", (Guid skillId, SkillInput input, ProfileService service, CancellationToken ct) => service.UpdateSkill(skillId, input, ct));
        group.MapDelete("/profile/skills/{skillId:guid}", async (Guid skillId, uint version, ProfileService service, CancellationToken ct) =>
        { await service.RemoveSkill(skillId, version, ct); return Results.NoContent(); });
        group.MapGet("/skills", (string? query, ProfileService service, CancellationToken ct) => service.Search(query, ct));
        group.MapGet("/account", (ProfileService service, CancellationToken ct) => service.Account(ct));
        group.MapPut("/account", (AccountInput input, ProfileService service, CancellationToken ct) => service.Rename(input, ct));
    }
    private static IResult Conflict() => Results.Problem(statusCode: 409, title: "This record changed or the skill is already added. Reload the latest data before editing again.");
}
