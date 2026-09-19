using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace CareerOS.Tests.Integration;

public sealed class ApiFoundationTests
{
    [Fact]
    public async Task LivenessWorksWithoutDependenciesAndPreservesCorrelationId()
    {
        await using var factory = new ApiFactory();
        using var client = factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/health/live");
        request.Headers.Add("X-Correlation-ID", "integration-123");
        using var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("integration-123", response.Headers.GetValues("X-Correlation-ID").Single());
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Healthy", body.GetProperty("status").GetString());
    }

    [Fact]
    public async Task ReadinessReportsUnavailableDependencies()
    {
        await using var factory = new ApiFactory();
        using var client = factory.CreateClient();
        using var response = await client.GetAsync("/health");
        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Unhealthy", body.GetProperty("checks").GetProperty("postgresql").GetString());
        Assert.Equal("Unhealthy", body.GetProperty("checks").GetProperty("redis").GetString());
        Assert.DoesNotContain("ConnectionStrings", body.ToString());
    }

    [Theory]
    [InlineData("/missing", HttpStatusCode.NotFound)]
    [InlineData("/test-only/throw", HttpStatusCode.InternalServerError)]
    public async Task ErrorsUseProblemDetailsWithoutSensitiveDetails(string path, HttpStatusCode status)
    {
        await using var factory = new ApiFactory();
        using var client = factory.CreateClient();
        using var response = await client.GetAsync(path);
        Assert.Equal(status, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal((int)status, body.GetProperty("status").GetInt32());
        Assert.Equal(response.Headers.GetValues("X-Correlation-ID").Single(), body.GetProperty("traceId").GetString());
        Assert.DoesNotContain("Sensitive test detail", body.ToString());
    }

    [Theory]
    [InlineData("http://localhost:3000", true)]
    [InlineData("https://untrusted.example", false)]
    public async Task CorsOnlyAllowsConfiguredOrigin(string origin, bool allowed)
    {
        await using var factory = new ApiFactory();
        using var client = factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Options, "/health/live");
        request.Headers.Add("Origin", origin);
        request.Headers.Add("Access-Control-Request-Method", "GET");
        using var response = await client.SendAsync(request);
        Assert.Equal(allowed, response.Headers.Contains("Access-Control-Allow-Origin"));
    }
}
