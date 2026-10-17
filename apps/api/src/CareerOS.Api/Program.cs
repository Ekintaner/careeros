using CareerOS.Infrastructure.Dashboard;
using CareerOS.Api.Assistance;
using CareerOS.Application.Assistance;
using CareerOS.Infrastructure.Assistance;
using CareerOS.Api.Analytics;
using CareerOS.Infrastructure.Analytics;
using CareerOS.Api.Planning;
using CareerOS.Infrastructure.Planning;
using CareerOS.Api.Intelligence;
using CareerOS.Infrastructure.Intelligence;
using CareerOS.Api.Matching;
using CareerOS.Infrastructure.Matching;
using CareerOS.Api.Documents;
using CareerOS.Infrastructure.Documents;
using System.Net;
using CareerOS.Api.Applications;
using CareerOS.Infrastructure.Applications;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Http.Features;
using CareerOS.Api.Authentication;
using CareerOS.Api.Profiles;
using CareerOS.Infrastructure.Profiles;
using CareerOS.Api.Opportunities;
using CareerOS.Infrastructure.Opportunities;
using CareerOS.Api.Observability;
using CareerOS.Infrastructure;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;

var builder = WebApplication.CreateBuilder(args);
builder.WebHost.ConfigureKestrel(options => options.Limits.MaxRequestBodySize = LocalDocumentStore.MaxBytes);
builder.Logging.ClearProviders();
builder.Logging.AddJsonConsole(options => options.IncludeScopes = true);
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddScoped<ProfileService>();
builder.Services.AddScoped<OpportunityService>();
builder.Services.AddScoped<ApplicationService>();
builder.Services.AddScoped<PlanningService>();
builder.Services.AddScoped<AnalyticsService>();
builder.Services.AddScoped<DashboardService>();
builder.Services.AddSingleton(_ => new LocalDocumentStore(builder.Configuration, builder.Environment.ContentRootPath));
builder.Services.AddScoped<DocumentService>();
builder.Services.AddScoped<MatchingService>();
builder.Services.AddScoped<IntelligenceService>();
builder.Services.AddSingleton<ILocalAiProvider>(_ => new LocalAiProvider(LocalAiOptions.Read(builder.Configuration), LocalAiProvider.CreateClient()));
builder.Services.AddScoped<AssistanceService>();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddProblemDetails(options =>
    options.CustomizeProblemDetails = context =>
    {
        context.ProblemDetails.Extensions["traceId"] = context.HttpContext.TraceIdentifier;
        context.ProblemDetails.Instance = context.HttpContext.Request.Path;
    });
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.AddCareerAuthentication();
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.KnownNetworks.Clear();
    options.KnownProxies.Clear();
    options.ForwardLimit = 1;
    var proxies = builder.Configuration.GetSection("Proxy:KnownProxies").Get<string[]>() ?? [];
    if (proxies.Length > 0)
    {
        options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
        foreach (var proxy in proxies)
            options.KnownProxies.Add(IPAddress.TryParse(proxy, out var address) ? address : throw new InvalidOperationException("Proxy:KnownProxies requires explicit IP addresses."));
    }
});

var app = builder.Build();
app.UseForwardedHeaders();
app.UseMiddleware<RequestLoggingMiddleware>();
app.UseExceptionHandler();
app.Use(async (context, next) =>
{
    // Bounded product text can require six bytes per UTF-16 unit in escaped JSON. Auth retains 16 KiB.
    var limit = context.Features.Get<IHttpMaxRequestBodySizeFeature>();
    var writing = context.Request.Method is "POST" or "PUT";
    var product = context.Request.Path.StartsWithSegments("/api/v1/interviews") || context.Request.Path.StartsWithSegments("/api/v1/tasks") || context.Request.Path.StartsWithSegments("/api/v1/jobs") || context.Request.Path.StartsWithSegments("/api/v1/companies") || context.Request.Path.StartsWithSegments("/api/v1/applications") || context.Request.Path.StartsWithSegments("/api/v1/resumes");
    if (limit is { IsReadOnly: false }) limit.MaxRequestBodySize = context.Request.Method == "POST" && context.Request.Path.Equals("/api/v1/documents", StringComparison.OrdinalIgnoreCase) ? LocalDocumentStore.MaxBytes : writing && product ? 384 * 1024 :
        context.Request.Method == "PUT" && context.Request.Path.Equals("/api/v1/profile", StringComparison.OrdinalIgnoreCase) ? 128 * 1024 : 16 * 1024;
    await next(context);
});
app.UseStatusCodePages();
app.UseCors();
app.UseCareerSecurity();
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.MapHealthChecks("/health", new HealthCheckOptions
{
    ResponseWriter = WriteHealthResponse
}).AllowAnonymous();
app.MapHealthChecks("/health/live", new HealthCheckOptions
{
    Predicate = _ => false,
    ResponseWriter = WriteHealthResponse
}).AllowAnonymous();

app.MapCareerAuth();
app.MapCareerProfile();
app.MapOpportunities();
app.MapApplications();
app.MapPlanning();
app.MapAnalytics();
app.MapGet("/api/v1/dashboard", (DashboardService service, CancellationToken ct) => service.Get(ct)).RequireAuthorization().WithTags("Dashboard");
app.MapDocuments();
app.MapMatching();
app.MapIntelligence();
app.MapAssistance();
app.Run();

static Task WriteHealthResponse(HttpContext context, HealthReport report) =>
    context.Response.WriteAsJsonAsync(new
    {
        status = report.Status.ToString(),
        checks = report.Entries.ToDictionary(entry => entry.Key, entry => entry.Value.Status.ToString()),
        traceId = context.TraceIdentifier
    }, context.RequestAborted);

public partial class Program;
