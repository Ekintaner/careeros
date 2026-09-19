using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace CareerOS.Tests.Integration;

internal sealed class ApiFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(
            new Dictionary<string, string?>
            {
                ["ConnectionStrings:PostgreSql"] = "Host=127.0.0.1;Port=1;Database=unavailable;Username=test;Timeout=1",
                ["ConnectionStrings:Redis"] = "127.0.0.1:1,connectTimeout=500,asyncTimeout=500",
                ["Cors:AllowedOrigins:0"] = "http://localhost:3000"
            }));
        builder.ConfigureServices(services => services.AddSingleton<IStartupFilter, ThrowingStartupFilter>());
    }

    private sealed class ThrowingStartupFilter : IStartupFilter
    {
        public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
        {
            next(app);
            app.Use(async (context, nextMiddleware) =>
            {
                if (context.Request.Path == "/test-only/throw") throw new InvalidOperationException("Sensitive test detail");
                await nextMiddleware(context);
            });
        };
    }
}
