using Microsoft.Extensions.Diagnostics.HealthChecks;
using StackExchange.Redis;

namespace CareerOS.Infrastructure.Health;

internal sealed class RedisHealthCheck(RedisConnection connection) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            var multiplexer = await connection.GetAsync(cancellationToken);
            await multiplexer.GetDatabase().PingAsync().WaitAsync(cancellationToken);
            return HealthCheckResult.Healthy();
        }
        catch (Exception exception) when (exception is RedisException or TimeoutException or OperationCanceledException)
        {
            return HealthCheckResult.Unhealthy("Redis is unavailable.");
        }
    }
}
