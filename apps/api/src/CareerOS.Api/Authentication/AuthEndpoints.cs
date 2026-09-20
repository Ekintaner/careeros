using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using CareerOS.Application.Authentication;
using CareerOS.Domain.Common;
using CareerOS.Infrastructure.Authentication;
using CareerOS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

namespace CareerOS.Api.Authentication;

public sealed record RegisterInput(string Email, string Password, string DisplayName);
public sealed record LoginInput(string Email, string Password);
public sealed record ForgotInput(string Email);
public sealed record ResetInput(string Token, string NewPassword);
public sealed record ChangeInput(string CurrentPassword, string NewPassword);

public static class AuthEndpoints
{
    public static void MapCareerAuth(this WebApplication app)
    {
        var group = app.MapGroup("/api/v1/auth").WithTags("Authentication");
        group.AddEndpointFilter(async (context, next) =>
        {
            try { return await next(context); }
            catch (DomainValidationException e) { return Results.Problem(statusCode: 400, title: e.Message); }
            catch (AuthenticationFailure e) { return Results.Problem(statusCode: 401, title: e.Message); }
            catch (DeliveryUnavailableException) { return Results.Problem(statusCode: 503, title: "Password recovery is currently unavailable. Please try again later."); }
        });
        group.MapPost("/register", async (RegisterInput input, AuthService service, AuthOptions options, HttpContext http, CancellationToken ct) =>
            Issue(http, await service.Register(input.Email, input.Password, input.DisplayName, options.RefreshTokenDays, ct)))
            .RequireRateLimiting("credentials");
        group.MapPost("/login", async (LoginInput input, AuthService service, AuthOptions options, HttpContext http, CancellationToken ct) =>
            Issue(http, await service.Login(input.Email, input.Password, options.RefreshTokenDays, ct)))
            .RequireRateLimiting("credentials");
        group.MapPost("/refresh", async (AuthService service, HttpContext http, CancellationToken ct) =>
        {
            try { return Issue(http, await service.Refresh(ReadRefresh(http), ct)); }
            catch (AuthenticationFailure) { Clear(http); throw; }
        }).RequireRateLimiting("refresh");
        group.MapGet("/me", async (ICurrentUser current, CareerOSDbContext db, CancellationToken ct) =>
            AccountView.From(await db.Users.AsNoTracking().SingleAsync(x => x.Id == current.UserId, ct))).RequireAuthorization();
        group.MapPost("/logout", async (AuthService service, HttpContext http, CancellationToken ct) =>
        {
            await service.Logout(ReadRefresh(http), ct);
            Clear(http);
            return Results.NoContent();
        }).RequireRateLimiting("refresh");
        group.MapPost("/logout-all", async (ICurrentUser current, AuthService service, HttpContext http, CancellationToken ct) =>
        {
            await service.LogoutAll(current.UserId, ct);
            Clear(http);
            return Results.NoContent();
        }).RequireAuthorization().RequireRateLimiting("credentials");
        group.MapPost("/change-password", async (ChangeInput input, ICurrentUser current, AuthService service, HttpContext http, CancellationToken ct) =>
        {
            await service.ChangePassword(current.UserId, input.CurrentPassword, input.NewPassword, ct);
            Clear(http);
            return Results.NoContent();
        }).RequireAuthorization().RequireRateLimiting("credentials");
        group.MapPost("/forgot-password", async (ForgotInput input, AuthService service, CancellationToken ct) =>
        {
            await service.ForgotPassword(input.Email, ct);
            return Results.Ok(new { message = "If an account exists, recovery instructions will be available through the configured delivery channel." });
        }).RequireRateLimiting("recovery");
        group.MapPost("/reset-password", async (ResetInput input, AuthService service, HttpContext http, CancellationToken ct) =>
        {
            await service.ResetPassword(input.Token, input.NewPassword, ct);
            Clear(http);
            return Results.NoContent();
        }).RequireRateLimiting("recovery");
    }
    private static bool Development(HttpContext http) => http.RequestServices.GetRequiredService<IHostEnvironment>().IsDevelopment();
    private static string? ReadRefresh(HttpContext http) => http.Request.Cookies[AuthSetup.RefreshCookie(Development(http))];
    private static CookieOptions Cookie(HttpContext http, string path, DateTimeOffset? expires = null) => new()
    { HttpOnly = true, Secure = !Development(http), SameSite = SameSiteMode.Strict, Path = path, Expires = expires, IsEssential = true };
    private static IResult Issue(HttpContext http, SessionCredentials session)
    {
        var options = http.RequestServices.GetRequiredService<AuthOptions>();
        var now = http.RequestServices.GetRequiredService<TimeProvider>().GetUtcNow().UtcDateTime;
        var expires = now.AddMinutes(options.AccessTokenMinutes);
        var jwt = new JwtSecurityToken(options.Issuer, options.Audience,
            [new Claim("sub", session.User.Id.ToString()), new Claim("sid", session.FamilyId.ToString()), new Claim("stamp", session.User.SecurityStamp.ToString())],
            now, expires, new SigningCredentials(new SymmetricSecurityKey(Convert.FromBase64String(options.SigningKey)), SecurityAlgorithms.HmacSha256));
        http.Response.Cookies.Append(AuthSetup.AccessCookie(Development(http)), new JwtSecurityTokenHandler().WriteToken(jwt), Cookie(http, "/", expires));
        http.Response.Cookies.Append(AuthSetup.RefreshCookie(Development(http)), session.RefreshSecret, Cookie(http, "/", session.ExpiresAtUtc));
        return Results.Ok(AccountView.From(session.User));
    }
    private static void Clear(HttpContext http)
    {
        http.Response.Cookies.Delete(AuthSetup.AccessCookie(Development(http)), Cookie(http, "/"));
        http.Response.Cookies.Delete(AuthSetup.RefreshCookie(Development(http)), Cookie(http, "/"));
    }
}
