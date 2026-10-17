using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Threading.RateLimiting;
using CareerOS.Application.Authentication;
using CareerOS.Domain.Accounts;
using CareerOS.Infrastructure.Authentication;
using CareerOS.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

namespace CareerOS.Api.Authentication;

public sealed class HttpCurrentUser(IHttpContextAccessor accessor) : ICurrentUser
{
    public Guid UserId => accessor.HttpContext?.User is { Identity.IsAuthenticated: true } principal
        && Guid.TryParse(principal.FindFirstValue("sub"), out var id) && id != Guid.Empty
        ? id : throw new UnauthorizedAccessException("An authenticated account is required.");
}

public static class AuthSetup
{
    public static void AddCareerAuthentication(this WebApplicationBuilder builder)
    {
        builder.Services.AddSingleton(provider =>
        {
            var config = provider.GetRequiredService<IConfiguration>().GetSection("Auth").Get<AuthOptions>() ?? new();
            config.Validate(provider.GetRequiredService<IHostEnvironment>().IsDevelopment());
            return config;
        });
        builder.Services.AddSingleton(TimeProvider.System);
        builder.Services.Configure<PasswordHasherOptions>(o => o.IterationCount = 210_000);
        builder.Services.AddSingleton<IPasswordHasher<User>, PasswordHasher<User>>();
        builder.Services.AddSingleton<DummyPassword>();
        builder.Services.AddScoped<AuthService>();
        builder.Services.AddHttpContextAccessor();
        builder.Services.AddScoped<ICurrentUser, HttpCurrentUser>();
        builder.Services.AddScoped<OwnedData>();
        builder.Services.AddSingleton<IPasswordResetDelivery>(provider =>
            provider.GetRequiredService<IHostEnvironment>().IsDevelopment() && provider.GetRequiredService<AuthOptions>().DevelopmentCapture
                ? new DevelopmentResetCapture() : new UnconfiguredResetDelivery());
        builder.Services.AddCors(options => options.AddDefaultPolicy(policy => policy.WithOrigins(Origins(builder.Configuration, builder.Environment))
            .AllowAnyHeader().AllowAnyMethod().AllowCredentials().WithExposedHeaders("X-Correlation-ID")));
        builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer();
        builder.Services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme).Configure<AuthOptions, IHostEnvironment>((options, config, environment) =>
        {
            options.MapInboundClaims = false;
            options.IncludeErrorDetails = false;
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidIssuer = config.Issuer,
                ValidateAudience = true,
                ValidAudience = config.Audience,
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(Convert.FromBase64String(config.SigningKey)),
                ValidAlgorithms = [SecurityAlgorithms.HmacSha256],
                ValidateLifetime = true,
                RequireExpirationTime = true,
                RequireSignedTokens = true,
                ClockSkew = TimeSpan.Zero
            };
            options.Events = new JwtBearerEvents
            {
                OnMessageReceived = context =>
                {
                    if (string.IsNullOrEmpty(context.Request.Headers.Authorization))
                        context.Token = context.Request.Cookies[AccessCookie(environment.IsDevelopment())];
                    return Task.CompletedTask;
                },
                OnTokenValidated = async context =>
                {
                    var principal = context.Principal!;
                    if (!Guid.TryParse(principal.FindFirstValue("sub"), out var id)
                        || !Guid.TryParse(principal.FindFirstValue("sid"), out var family)
                        || !Guid.TryParse(principal.FindFirstValue("stamp"), out var stamp)) { context.Fail("Invalid session."); return; }
                    var db = context.HttpContext.RequestServices.GetRequiredService<CareerOSDbContext>();
                    var now = context.HttpContext.RequestServices.GetRequiredService<TimeProvider>().GetUtcNow().UtcDateTime;
                    if (!await db.Users.AnyAsync(u => u.Id == id && u.SecurityStamp == stamp && u.PasswordHash != null, context.HttpContext.RequestAborted)
                        || !await db.RefreshTokens.AnyAsync(t => t.UserId == id && t.FamilyId == family && t.RevokedAtUtc == null && t.ExpiresAtUtc > now, context.HttpContext.RequestAborted))
                        context.Fail("Invalid session.");
                }
            };
        });
        builder.Services.AddAuthorization();
        builder.Services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = 429;
            options.OnRejected = async (context, ct) =>
            {
                context.HttpContext.Response.Headers.RetryAfter = "60";
                await Results.Problem(statusCode: 429, title: "Too many attempts. Try again shortly.").ExecuteAsync(context.HttpContext);
            };
            foreach (var (name, permits) in new[] { ("credentials", 10), ("recovery", 5), ("refresh", 60), ("local-ai", 6), ("local-ai-status", 30) })
                options.AddPolicy(name, context => RateLimitPartition.GetFixedWindowLimiter(
                    context.Connection.RemoteIpAddress?.ToString() ?? "unknown", _ => new FixedWindowRateLimiterOptions
                    { PermitLimit = permits, Window = TimeSpan.FromMinutes(1), QueueLimit = 0, AutoReplenishment = true }));
        });
    }
    private static string[] Origins(IConfiguration configuration, IHostEnvironment environment)
    {
        var origins = configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
        if (origins.Length == 0 || origins.Any(origin => !Uri.TryCreate(origin, UriKind.Absolute, out var uri)
            || uri.GetLeftPart(UriPartial.Authority) != origin || !string.IsNullOrEmpty(uri.UserInfo)
            || (uri.Scheme != "https" && !(environment.IsDevelopment() && uri.Scheme == "http" && uri.IsLoopback))))
            throw new InvalidOperationException("Configure explicit HTTPS frontend origins (loopback HTTP allowed in Development).");
        return origins;
    }
    public static string AccessCookie(bool development) => development ? "careeros-access" : "__Host-careeros-access";
    public static string RefreshCookie(bool development) => development ? "careeros-refresh" : "__Host-careeros-refresh";

    public static void UseCareerSecurity(this WebApplication app)
    {
        _ = app.Services.GetRequiredService<AuthOptions>();
        var origins = Origins(app.Configuration, app.Environment);
        app.Use(async (context, next) =>
        {
            context.Response.Headers.XContentTypeOptions = "nosniff";
            context.Response.Headers["Referrer-Policy"] = "no-referrer";
            if (!app.Environment.IsDevelopment()) context.Response.Headers.StrictTransportSecurity = "max-age=31536000";
            if (context.Request.Path.StartsWithSegments("/api"))
            {
                context.Response.Headers.CacheControl = "no-store";
                context.Response.Headers.Pragma = "no-cache";
                if (context.Request.Method is not ("GET" or "HEAD" or "OPTIONS")
                    && (context.Request.Headers["X-CareerOS-CSRF"] != "1"
                        || !origins.Contains(context.Request.Headers.Origin.ToString(), StringComparer.Ordinal)))
                {
                    await Results.Problem(statusCode: 403, title: "Request origin could not be verified.").ExecuteAsync(context);
                    return;
                }
            }
            await next(context);
        });
        app.UseRateLimiter();
        app.UseAuthentication();
        app.UseAuthorization();
    }
}
