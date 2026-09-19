namespace CareerOS.Api.Observability;

public static class CorrelationId
{
    public const string HeaderName = "X-Correlation-ID";

    public static string Resolve(string? incoming) =>
        incoming is { Length: > 0 and <= 64 } && incoming.All(character =>
            char.IsAsciiLetterOrDigit(character) || character is '-' or '_' or '.')
            ? incoming
            : Guid.NewGuid().ToString("N");
}
