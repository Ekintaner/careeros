using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using CareerOS.Application.Authentication;
using CareerOS.Domain.Accounts;
using CareerOS.Infrastructure.Authentication;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.IdentityModel.Tokens;
using Xunit;

namespace CareerOS.Tests.Integration;

[Collection("PostgreSQL")]
public sealed class AuthTests(PostgreSqlFixture fixture)
{
    private const string Password = "several ordinary memorable words";
    private const string NewPassword = "another long memorable passphrase";
    private static Task<HttpResponseMessage> Register(Browser browser, string email = "person@example.com") => browser.Send("register", new { email, password = Password, displayName = "Person" });
    private static Task<HttpResponseMessage> Login(Browser browser, string password = Password, string email = "person@example.com") => browser.Send("login", new { email, password });
    private async Task<TestDatabase> Database()
    {
        var database = await fixture.CreateDatabaseAsync();
        await using var db = database.Context(); await db.Database.MigrateAsync(); return database;
    }
    [PostgreSqlFact]
    public async Task RegistrationIsAtomicNormalizedAndReturnsOnlySafeAccount()
    {
        await using var database = await Database(); using var factory = new AuthFactory(database.ConnectionString); using var browser = factory.Browser();
        var result = await Register(browser, " PERSON@example.com "); Assert.Equal(HttpStatusCode.OK, result.StatusCode);
        var body = await result.Content.ReadAsStringAsync(); Assert.DoesNotContain("hash", body, StringComparison.OrdinalIgnoreCase); Assert.DoesNotContain("stamp", body, StringComparison.OrdinalIgnoreCase);
        var account = await result.Content.ReadFromJsonAsync<AccountView>(); Assert.Equal("Person", account!.DisplayName); Assert.False(account.EmailVerified);
        Assert.All(result.Headers.GetValues("Set-Cookie"), cookie => { Assert.Contains("httponly", cookie); Assert.Contains("samesite=strict", cookie); });
        Assert.Equal("no-store", result.Headers.CacheControl!.ToString());
        await using var db = database.Context(); var user = await db.Users.SingleAsync(); Assert.NotEqual(Password, user.PasswordHash); Assert.NotNull(user.LastLoginAtUtc);
        Assert.Single(await db.CareerProfiles.ToListAsync()); var token = await db.RefreshTokens.SingleAsync(); Assert.NotEqual(browser.Refresh, token.TokenHash); Assert.Equal(SecurityTokens.Hash(browser.Refresh), token.TokenHash);
        Assert.Equal(HttpStatusCode.BadRequest, (await Register(browser)).StatusCode); Assert.Equal(1, await db.Users.CountAsync());
    }
    [PostgreSqlFact]
    public async Task LoginAndMeRequireValidCredentialsWithoutEnumeratingAccounts()
    {
        await using var database = await Database(); using var factory = new AuthFactory(database.ConnectionString); using var browser = factory.Browser();
        Assert.Equal(HttpStatusCode.Unauthorized, (await browser.Send("me", post: false)).StatusCode);
        await Register(browser);
        using var stranger = factory.Browser(); var wrong = await Login(stranger, "incorrect"); var missing = await Login(stranger, "incorrect", "missing@example.com");
        Assert.Equal(HttpStatusCode.Unauthorized, wrong.StatusCode); Assert.Equal(wrong.StatusCode, missing.StatusCode);
        Assert.Equal((await wrong.Content.ReadFromJsonAsync<Dictionary<string, object>>())!["title"].ToString(), (await missing.Content.ReadFromJsonAsync<Dictionary<string, object>>())!["title"].ToString());
        Assert.Equal(HttpStatusCode.OK, (await Login(stranger, email: "PERSON@EXAMPLE.COM")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await stranger.Send("me", post: false)).StatusCode);
    }
    [PostgreSqlFact]
    public async Task RefreshRotatesAndReplayRevokesFamilyButNotOtherDevice()
    {
        await using var database = await Database(); using var factory = new AuthFactory(database.ConnectionString); using var browser = factory.Browser(); using var other = factory.Browser();
        await Register(browser); await Login(other); var old = browser.Refresh;
        Assert.Equal(HttpStatusCode.OK, (await browser.Send("refresh")).StatusCode); var latest = browser.Refresh; Assert.NotEqual(old, latest);
        await using (var db = database.Context()) { var rows = await db.RefreshTokens.OrderBy(x => x.CreatedAtUtc).ToListAsync(); Assert.Equal(3, rows.Count); Assert.Contains(rows, x => x.ReplacedByTokenId.HasValue && x.RevokedAtUtc.HasValue); }
        using var replay = factory.Browser(); replay.Cookies["careeros-refresh"] = old;
        Assert.Equal(HttpStatusCode.Unauthorized, (await replay.Send("refresh")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await browser.Send("me", post: false)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await browser.Send("refresh")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await other.Send("me", post: false)).StatusCode);
    }
    [PostgreSqlFact]
    public async Task ConcurrentRotationHasOneWinnerAndRevokesOnReuse()
    {
        await using var database = await Database(); using var factory = new AuthFactory(database.ConnectionString); using var a = factory.Browser(); using var b = factory.Browser();
        await Register(a); b.Cookies["careeros-refresh"] = a.Refresh;
        var results = await Task.WhenAll(a.Send("refresh"), b.Send("refresh"));
        Assert.Single(results, x => x.StatusCode == HttpStatusCode.OK); Assert.Single(results, x => x.StatusCode == HttpStatusCode.Unauthorized);
        await using var db = database.Context(); Assert.False(await db.RefreshTokens.AnyAsync(x => x.RevokedAtUtc == null));
    }
    [PostgreSqlFact]
    public async Task LogoutIsIdempotentRevokesAccessAndRefresh()
    {
        await using var database = await Database(); using var factory = new AuthFactory(database.ConnectionString); using var browser = factory.Browser(); using var stolen = factory.Browser();
        await Register(browser); foreach (var pair in browser.Cookies) stolen.Cookies[pair.Key] = pair.Value;
        Assert.Equal(HttpStatusCode.NoContent, (await browser.Send("logout")).StatusCode); Assert.Empty(browser.Cookies);
        Assert.Equal(HttpStatusCode.NoContent, (await browser.Send("logout")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await stolen.Send("me", post: false)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await stolen.Send("refresh")).StatusCode);
    }
    [PostgreSqlFact]
    public async Task LogoutAllAndPasswordChangeInvalidateEverySession()
    {
        await using var database = await Database(); using var factory = new AuthFactory(database.ConnectionString); using var a = factory.Browser(); using var b = factory.Browser();
        await Register(a); await Login(b);
        Assert.Equal(HttpStatusCode.NoContent, (await a.Send("logout-all")).StatusCode); Assert.Equal(HttpStatusCode.Unauthorized, (await b.Send("refresh")).StatusCode);
        await Login(a); await Login(b);
        Assert.Equal(HttpStatusCode.Unauthorized, (await a.Send("change-password", new { currentPassword = "wrong", newPassword = NewPassword })).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await a.Send("change-password", new { currentPassword = Password, newPassword = NewPassword })).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await b.Send("me", post: false)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await Login(a)).StatusCode); Assert.Equal(HttpStatusCode.OK, (await Login(a, NewPassword)).StatusCode);
    }
    [PostgreSqlFact]
    public async Task PasswordResetIsHashedSingleUseAndRevokesAllSessions()
    {
        await using var database = await Database(); using var factory = new AuthFactory(database.ConnectionString); using var a = factory.Browser(); using var b = factory.Browser();
        await Register(a); await Login(b);
        var known = await a.Send("forgot-password", new { email = "person@example.com" }); var unknown = await a.Send("forgot-password", new { email = "unknown@example.com" });
        Assert.Equal(HttpStatusCode.OK, known.StatusCode); Assert.Equal(await known.Content.ReadAsStringAsync(), await unknown.Content.ReadAsStringAsync());
        var secret = factory.TakeReset("person@example.com"); Assert.NotNull(secret);
        await using (var db = database.Context()) Assert.Equal(SecurityTokens.Hash(secret), (await db.PasswordResetTokens.SingleAsync()).TokenHash);
        Assert.Equal(HttpStatusCode.NoContent, (await a.Send("reset-password", new { token = secret, newPassword = NewPassword })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await a.Send("reset-password", new { token = secret, newPassword = NewPassword })).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await b.Send("refresh")).StatusCode); Assert.Equal(HttpStatusCode.Unauthorized, (await Login(a)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await Login(a, NewPassword)).StatusCode);
    }
    [PostgreSqlFact]
    public async Task ExpiredRefreshAndResetCannotBeUsed()
    {
        await using var database = await Database(); using var factory = new AuthFactory(database.ConnectionString); using var browser = factory.Browser();
        await Register(browser); await browser.Send("forgot-password", new { email = "person@example.com" }); var secret = factory.TakeReset("person@example.com");
        factory.Clock.Now = factory.Clock.Now.AddDays(31);
        Assert.Equal(HttpStatusCode.BadRequest, (await browser.Send("reset-password", new { token = secret, newPassword = NewPassword })).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await browser.Send("refresh")).StatusCode);
    }
    [PostgreSqlFact]
    public async Task RecoveryUnavailableFailsEquallyForKnownAndUnknownAccounts()
    {
        await using var database = await Database(); using var factory = new AuthFactory(database.ConnectionString, capture: false); using var browser = factory.Browser();
        await Register(browser);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, (await browser.Send("forgot-password", new { email = "person@example.com" })).StatusCode);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, (await browser.Send("forgot-password", new { email = "unknown@example.com" })).StatusCode);
        await using var db = database.Context(); Assert.Empty(await db.PasswordResetTokens.ToListAsync());
    }
    [PostgreSqlFact]
    public async Task InvalidTamperedExpiredIssuerAndAudienceTokensAreRejected()
    {
        await using var database = await Database(); using var factory = new AuthFactory(database.ConnectionString); using var browser = factory.Browser(); await Register(browser);
        var parsed = new JwtSecurityTokenHandler().ReadJwtToken(browser.Access);
        foreach (var variant in new[] { "tampered", "expired", "issuer", "audience" })
        {
            using var attacker = factory.Browser();
            var token = new JwtSecurityToken(variant == "issuer" ? "other" : "tests", variant == "audience" ? "other" : "tests", parsed.Claims.Where(x => x.Type is "sub" or "sid" or "stamp"),
                DateTime.UtcNow.AddMinutes(-10), variant == "expired" ? DateTime.UtcNow.AddMinutes(-1) : DateTime.UtcNow.AddMinutes(5),
                new SigningCredentials(new SymmetricSecurityKey(variant == "tampered" ? System.Security.Cryptography.RandomNumberGenerator.GetBytes(32) : Convert.FromBase64String(factory.SigningKey)), SecurityAlgorithms.HmacSha256));
            attacker.Cookies["careeros-access"] = new JwtSecurityTokenHandler().WriteToken(token);
            Assert.Equal(HttpStatusCode.Unauthorized, (await attacker.Send("me", post: false)).StatusCode);
        }
    }
    [PostgreSqlFact]
    public async Task SessionPersistsAcrossHostAndDbContextLifecycles()
    {
        await using var database = await Database(); string key; Dictionary<string, string> cookies;
        using (var factory = new AuthFactory(database.ConnectionString)) { key = factory.SigningKey; using var browser = factory.Browser(); await Register(browser); cookies = new(browser.Cookies); }
        using var restarted = new AuthFactory(database.ConnectionString, signingKey: key); using var restored = restarted.Browser();
        foreach (var pair in cookies) restored.Cookies[pair.Key] = pair.Value;
        Assert.Equal(HttpStatusCode.OK, (await restored.Send("me", post: false)).StatusCode); Assert.Equal(HttpStatusCode.OK, (await restored.Send("refresh")).StatusCode);
    }
    [PostgreSqlFact]
    public async Task MeUsesPrincipalAndCannotSelectAnotherAccount()
    {
        await using var database = await Database(); using var factory = new AuthFactory(database.ConnectionString); using var a = factory.Browser(); using var b = factory.Browser();
        var accountA = await (await Register(a)).Content.ReadFromJsonAsync<AccountView>(); var accountB = await (await Register(b, "b@example.com")).Content.ReadFromJsonAsync<AccountView>();
        var me = await (await a.Send("me?userId=" + accountB!.Id, post: false)).Content.ReadFromJsonAsync<AccountView>(); Assert.Equal(accountA!.Id, me!.Id);
        await a.Send("logout-all", new { userId = accountB.Id }); Assert.Equal(HttpStatusCode.OK, (await b.Send("me", post: false)).StatusCode);
    }
    [PostgreSqlFact]
    public async Task CsrfAndRateLimitingAreEnforced()
    {
        await using var database = await Database(); using var factory = new AuthFactory(database.ConnectionString); using var browser = factory.Browser();
        Assert.Equal(HttpStatusCode.Forbidden, (await browser.Send("login", new { email = "x@example.com", password = Password }, csrf: false)).StatusCode);
        for (var i = 0; i < 10; i++) Assert.Equal(HttpStatusCode.Unauthorized, (await browser.Send("login", new { email = "person@example.com", password = Password }, forwardedFor: $"198.51.100.{i + 1}")).StatusCode);
        var limited = await Login(browser); Assert.Equal(HttpStatusCode.TooManyRequests, limited.StatusCode); Assert.NotNull(limited.Headers.RetryAfter);
    }
    [PostgreSqlFact]
    public async Task ProductionCookiesAreSecureAndCaptureCannotBeEnabled()
    {
        await using var database = await Database();
        using var factory = new AuthFactory(database.ConnectionString, capture: false, production: true); using var browser = factory.Browser();
        var response = await Register(browser); Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var cookies = response.Headers.GetValues("Set-Cookie").ToArray();
        Assert.All(cookies, value => { Assert.Contains("secure", value); Assert.Contains("httponly", value); Assert.Contains("samesite=strict", value); Assert.DoesNotContain("domain=", value); });
        Assert.Contains(cookies, value => value.StartsWith("__Host-careeros-access=", StringComparison.Ordinal) && value.Contains("path=/;", StringComparison.Ordinal));
        Assert.Contains(cookies, value => value.StartsWith("__Host-careeros-refresh=", StringComparison.Ordinal) && value.Contains("path=/;", StringComparison.Ordinal));
        Assert.True(response.Headers.Contains("Strict-Transport-Security"));
        using var invalid = new AuthFactory(database.ConnectionString, capture: true, production: true);
        Assert.Throws<InvalidOperationException>(() => invalid.Browser());
    }

    [PostgreSqlFact]
    public async Task CustomHeaderDoesNotAuthorizeAnUntrustedOrigin()
    {
        await using var database = await Database(); using var factory = new AuthFactory(database.ConnectionString);
        using var client = factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/refresh");
        request.Headers.Add("Origin", "https://attacker.example"); request.Headers.Add("X-CareerOS-CSRF", "1");
        var response = await client.SendAsync(request); Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.False(response.Headers.Contains("Access-Control-Allow-Origin"));
    }

    [PostgreSqlFact]
    public async Task MigrationUpgradesExistingUsersAndRollsBackWithoutTouchingDomainSchema()
    {
        await using var database = await fixture.CreateDatabaseAsync(); await using var db = database.Context();
        await db.GetService<IMigrator>().MigrateAsync("InitialDomainSchema");
        var id = Guid.NewGuid(); await database.ExecuteAsync($"INSERT INTO users (\"Id\", \"Email\", \"DisplayName\") VALUES ('{id}', 'legacy@example.com', 'Legacy')");
        await db.Database.MigrateAsync(); var legacy = await db.Users.SingleAsync(); Assert.Equal(id, legacy.Id); Assert.Null(legacy.PasswordHash);
        Assert.True(await database.ScalarAsync<bool>("SELECT EXISTS (SELECT 1 FROM pg_indexes WHERE tablename='refresh_tokens' AND indexdef LIKE '%UNIQUE%' AND indexdef LIKE '%TokenHash%')"));
        Assert.True(await database.ScalarAsync<bool>("SELECT EXISTS (SELECT 1 FROM pg_indexes WHERE tablename='password_reset_tokens' AND indexdef LIKE '%ExpiresAtUtc%')"));
        await db.GetService<IMigrator>().MigrateAsync("InitialDomainSchema"); Assert.Equal(17L, await database.ScalarAsync<long>("SELECT count(*) FROM information_schema.tables WHERE table_schema='public' AND table_name <> '__EFMigrationsHistory'"));
        await db.Database.MigrateAsync(); Assert.False(db.Database.HasPendingModelChanges());
    }
}
