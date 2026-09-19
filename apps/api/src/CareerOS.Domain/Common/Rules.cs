using System.Net.Mail;

namespace CareerOS.Domain.Common;

internal static class Rules
{
    public static Guid Id(Guid value, string name)
    {
        if (value == Guid.Empty) throw new DomainValidationException($"{name} cannot be empty.");
        return value;
    }

    public static string Required(string value, int max, string name)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Trim().Length > max)
            throw new DomainValidationException($"{name} must contain 1–{max} characters.");
        return value.Trim();
    }

    public static string? Optional(string? value, int max, string name) =>
        string.IsNullOrWhiteSpace(value) ? null : Required(value, max, name);

    // ASCII-only case folding keeps .NET and PostgreSQL's translate() normalization identical.
    public static string Normalize(string value) =>
        string.Concat(value.Select(c => c is >= 'A' and <= 'Z' ? (char)(c + 32) : c));

    public static string Email(string value)
    {
        var email = Required(value, 254, nameof(value));
        if (!MailAddress.TryCreate(email, out var parsed) || parsed.Address != email || !email.Contains('@'))
            throw new DomainValidationException("Email must be a bare email address.");
        if (email.Any(c => c > 127 || char.IsWhiteSpace(c)))
            throw new DomainValidationException("Use an ASCII email address (punycode domains are supported).");
        return email;
    }

    public static string? Url(string? value)
    {
        var url = Optional(value, 2048, nameof(value));
        if (url is not null && (url.Any(char.IsWhiteSpace) || !Uri.TryCreate(url, UriKind.Absolute, out var uri)
            || uri.Scheme is not ("http" or "https") || string.IsNullOrEmpty(uri.Host)
            || !string.IsNullOrEmpty(uri.UserInfo)))
            throw new DomainValidationException("URLs must use HTTP(S), include a host and exclude credentials.");
        return url;
    }

    public static T EnumValue<T>(T value) where T : struct, Enum =>
        Enum.IsDefined(value) ? value : throw new DomainValidationException($"Invalid {typeof(T).Name}.");

    public static DateTime Utc(DateTime value)
    {
        if (value.Kind != DateTimeKind.Utc) throw new DomainValidationException("Timestamp must be UTC.");
        return value;
    }

    public static DateTime? Utc(DateTime? value) => value.HasValue ? Utc(value.Value) : null;

    public static decimal? Experience(decimal? value)
    {
        if (value is < 0 or > 100 || (value.HasValue && decimal.Round(value.Value, 1) != value))
            throw new DomainValidationException("Experience must be 0–100 years with at most one decimal place.");
        return value;
    }

    public static void SameOwner(Guid expected, Guid actual)
    {
        if (expected != actual) throw new DomainValidationException("Related records must belong to the same user.");
    }
}
