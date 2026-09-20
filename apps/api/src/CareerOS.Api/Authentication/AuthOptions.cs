namespace CareerOS.Api.Authentication;

public sealed class AuthOptions
{
    public string Issuer { get; set; } = "";
    public string Audience { get; set; } = "";
    public string SigningKey { get; set; } = "";
    public int AccessTokenMinutes { get; set; } = 5;
    public int RefreshTokenDays { get; set; } = 30;
    public bool DevelopmentCapture { get; set; }
    public void Validate(bool development)
    {
        byte[] key;
        try { key = Convert.FromBase64String(SigningKey); }
        catch (FormatException) { throw new InvalidOperationException("Auth:SigningKey must be a base64-encoded random key of at least 32 bytes."); }
        if (key.Length < 32 || string.IsNullOrWhiteSpace(Issuer) || string.IsNullOrWhiteSpace(Audience)
            || AccessTokenMinutes is < 1 or > 15 || RefreshTokenDays is < 1 or > 90 || (!development && DevelopmentCapture))
            throw new InvalidOperationException("Invalid authentication configuration. Supply issuer, audience, signing key and bounded lifetimes. Capture is development-only.");
    }
}
