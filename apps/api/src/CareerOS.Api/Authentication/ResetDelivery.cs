using System.Collections.Concurrent;
using CareerOS.Application.Authentication;

namespace CareerOS.Api.Authentication;

public sealed class UnconfiguredResetDelivery : IPasswordResetDelivery
{
    public bool IsAvailable => false;
    public Task SendAsync(string email, string token, CancellationToken cancellationToken) => throw new DeliveryUnavailableException();
}

// Opt-in Development only. Inspect via a debugger or test DI; no HTTP endpoint, console or disk output.
public sealed class DevelopmentResetCapture : IPasswordResetDelivery
{
    private readonly ConcurrentDictionary<string, (string Token, DateTimeOffset Expires)> messages = new();
    public bool IsAvailable => true;
    public Task SendAsync(string email, string token, CancellationToken cancellationToken)
    {
        foreach (var item in messages.Where(x => x.Value.Expires < DateTimeOffset.UtcNow)) messages.TryRemove(item.Key, out _);
        if (messages.Count >= 100) throw new DeliveryUnavailableException();
        messages[email] = (token, DateTimeOffset.UtcNow.AddMinutes(30));
        return Task.CompletedTask;
    }
    public string? Take(string email) => messages.TryRemove(email, out var item) && item.Expires > DateTimeOffset.UtcNow ? item.Token : null;
}
