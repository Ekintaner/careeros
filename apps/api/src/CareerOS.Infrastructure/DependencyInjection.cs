using CareerOS.Infrastructure.Health;
using CareerOS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Npgsql;
using StackExchange.Redis;

namespace CareerOS.Infrastructure;

public sealed class DependencyOptions
{
    public string PostgreSql { get; set; } = "";
    public string Redis { get; set; } = "";

    public bool IsValid()
    {
        try
        {
            var postgres = new NpgsqlConnectionStringBuilder(PostgreSql);
            var redis = ConfigurationOptions.Parse(Redis);
            return !string.IsNullOrWhiteSpace(postgres.Host)
                && !string.IsNullOrWhiteSpace(postgres.Database)
                && !string.IsNullOrWhiteSpace(postgres.Username)
                && redis.EndPoints.Count > 0;
        }
        catch (Exception exception) when (exception is ArgumentException or FormatException)
        {
            return false;
        }
    }
}

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<DependencyOptions>()
            .Bind(configuration.GetSection("ConnectionStrings"))
            .Validate(options => options.IsValid(), "Valid PostgreSql and Redis connection strings are required.")
            .ValidateOnStart();

        services.AddDbContext<CareerOSDbContext>((provider, options) =>
            options.UseNpgsql(provider.GetRequiredService<IOptions<DependencyOptions>>().Value.PostgreSql));

        services.AddSingleton<RedisConnection>();
        services.AddHealthChecks()
            .AddCheck<PostgreSqlHealthCheck>("postgresql", tags: ["ready"], timeout: TimeSpan.FromSeconds(5))
            .AddCheck<RedisHealthCheck>("redis", tags: ["ready"], timeout: TimeSpan.FromSeconds(5));
        return services;
    }
}
