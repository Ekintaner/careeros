using System.Net.Http.Json;
using System.Security.Cryptography;
using CareerOS.Api.Authentication;
using CareerOS.Application.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CareerOS.Tests.Integration;

internal sealed class AuthFactory(string connection, bool capture = true, string? signingKey = null, bool production = false) : WebApplicationFactory<Program>
{
    public string SigningKey { get; } = signingKey ?? Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
    public TestClock Clock { get; } = new();
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment(production ? "Production" : "Development");
        builder.ConfigureAppConfiguration((_, c) => c.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:PostgreSql"] = connection,
            ["ConnectionStrings:Redis"] = "127.0.0.1:1,connectTimeout=500,asyncTimeout=500",
            ["Auth:Issuer"] = "tests",
            ["Auth:Audience"] = "tests",
            ["Auth:SigningKey"] = SigningKey,
            ["Auth:DevelopmentCapture"] = capture.ToString(),
            ["Cors:AllowedOrigins:0"] = production ? "https://careeros.example" : "http://localhost:3000"
        }));
        builder.ConfigureServices(s => { s.RemoveAll<TimeProvider>(); s.AddSingleton<TimeProvider>(Clock); });
    }
    public Browser Browser() => new(CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false, AllowAutoRedirect = false }), production ? "https://careeros.example" : "http://localhost:3000");
    public string? TakeReset(string email) => ((DevelopmentResetCapture)Services.GetRequiredService<IPasswordResetDelivery>()).Take(email);
}
internal sealed class TestClock : TimeProvider
{
    public DateTimeOffset Now { get; set; } = DateTimeOffset.UtcNow;
    public override DateTimeOffset GetUtcNow() => Now;
}
internal sealed class Browser(HttpClient client, string origin = "http://localhost:3000") : IDisposable
{
    public Dictionary<string, string> Cookies { get; } = new();
    public string Refresh => Cookies["careeros-refresh"];
    public string Access => Cookies["careeros-access"];
    public async Task<HttpResponseMessage> Api(string path, HttpMethod? method = null, object? body = null, bool csrf = true)
    {
        using var request = new HttpRequestMessage(method ?? HttpMethod.Get, "/api/v1/" + path);
        if (csrf) { request.Headers.Add("Origin", origin); request.Headers.Add("X-CareerOS-CSRF", "1"); }
        if (Cookies.Count > 0) request.Headers.Add("Cookie", string.Join("; ", Cookies.Select(x => $"{x.Key}={x.Value}")));
        if (body is not null) request.Content = JsonContent.Create(body);
        return await client.SendAsync(request);
    }
    public async Task<HttpResponseMessage> Send(string path, object? body = null, bool post = true, bool csrf = true, string? forwardedFor = null)
    {
        using var request = new HttpRequestMessage(post ? HttpMethod.Post : HttpMethod.Get, "/api/v1/auth/" + path);
        if (csrf) { request.Headers.Add("Origin", origin); request.Headers.Add("X-CareerOS-CSRF", "1"); }
        if (forwardedFor is not null) request.Headers.Add("X-Forwarded-For", forwardedFor);
        if (Cookies.Count > 0) request.Headers.Add("Cookie", string.Join("; ", Cookies.Select(x => $"{x.Key}={x.Value}")));
        if (body is not null) request.Content = JsonContent.Create(body);
        var response = await client.SendAsync(request);
        if (response.Headers.TryGetValues("Set-Cookie", out var values))
            foreach (var value in values) { var pair = value.Split(';')[0].Split('=', 2); if (pair[1] == "") Cookies.Remove(pair[0]); else Cookies[pair[0]] = pair[1]; }
        return response;
    }
    public void Dispose() => client.Dispose();
}
