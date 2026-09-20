using System.Net;
using Microsoft.AspNetCore.HttpOverrides;
using CareerOS.Api.Authentication;
using CareerOS.Api.Observability;
using CareerOS.Infrastructure;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;

var builder = WebApplication.CreateBuilder(args);
builder.WebHost.ConfigureKestrel(options => options.Limits.MaxRequestBodySize = 16 * 1024);
builder.Logging.ClearProviders();
builder.Logging.AddJsonConsole(options => options.IncludeScopes = true);
builder.Services.AddInfrastructure(builder.Configuration);
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
app.Run();

static Task WriteHealthResponse(HttpContext context, HealthReport report) =>
    context.Response.WriteAsJsonAsync(new
    {
        status = report.Status.ToString(),
        checks = report.Entries.ToDictionary(entry => entry.Key, entry => entry.Value.Status.ToString()),
        traceId = context.TraceIdentifier
    }, context.RequestAborted);

public partial class Program;
