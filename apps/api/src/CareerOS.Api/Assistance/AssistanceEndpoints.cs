using CareerOS.Application.Assistance;
using CareerOS.Application.Applications;
using CareerOS.Domain.Common;
using CareerOS.Infrastructure.Assistance;
namespace CareerOS.Api.Assistance;
public static class AssistanceEndpoints
{
    public static void MapAssistance(this WebApplication app)
    {
        var g=app.MapGroup("/api/v1").RequireAuthorization().WithTags("Optional local assistance");
        g.MapGet("/ai/status",(ILocalAiProvider provider,CancellationToken ct)=>provider.Status(ct)).RequireRateLimiting("local-ai-status");
        g.MapPost("/jobs/{id:guid}/assistance",async (Guid id,AssistanceInput input,AssistanceService service,CancellationToken ct)=> {
            try {return Results.Ok(await service.Generate(id,input,ct));}
            catch(DomainValidationException e) {return Results.Problem(statusCode:400,title:e.Message);}
            catch(ApplicationNotFoundException) {return Results.NotFound();}
            catch(AiUnavailableException e) {return Results.Problem(statusCode:503,title:e.Message);}
            catch(AiOutputException) {return Results.Problem(statusCode:502,title:"The model output could not be verified. No suggestions were accepted and your records were not changed.");}
        }).RequireRateLimiting("local-ai");
    }
}
