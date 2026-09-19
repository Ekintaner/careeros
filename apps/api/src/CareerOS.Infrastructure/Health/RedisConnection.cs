using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace CareerOS.Infrastructure.Health;

/// <summary>Owns one asynchronously created multiplexer for the process lifetime.</summary>
public sealed class RedisConnection(IOptions<DependencyOptions> options) : IAsyncDisposable
{
    private readonly Lazy<Task<ConnectionMultiplexer>> connection = new(() =>
    {
        var configuration = ConfigurationOptions.Parse(options.Value.Redis);
        configuration.AbortOnConnectFail = false;
        configuration.ConnectTimeout = 2000;
        configuration.AsyncTimeout = 2000;
        configuration.ConnectRetry = 0;
        return ConnectionMultiplexer.ConnectAsync(configuration);
    });

    public async Task<IConnectionMultiplexer> GetAsync(CancellationToken cancellationToken = default) =>
        await connection.Value.WaitAsync(cancellationToken);

    public async ValueTask DisposeAsync()
    {
        if (!connection.IsValueCreated) return;
        try
        {
            var multiplexer = await connection.Value;
            await multiplexer.DisposeAsync();
        }
        catch (RedisConnectionException) { /* A failed connection owns no live multiplexer. */ }
    }
}
