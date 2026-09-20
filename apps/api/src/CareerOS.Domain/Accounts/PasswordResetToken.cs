using CareerOS.Domain.Common;

namespace CareerOS.Domain.Accounts;

public sealed class PasswordResetToken
{
    private PasswordResetToken() { }
    public PasswordResetToken(Guid userId, string hash, DateTime now, DateTime expires)
    {
        Id = Guid.NewGuid();
        UserId = Rules.Id(userId, nameof(userId));
        TokenHash = Rules.Required(hash, 64, nameof(hash));
        CreatedAtUtc = Rules.Utc(now);
        ExpiresAtUtc = Rules.Utc(expires);
        if (expires <= now) throw new DomainValidationException("Token expiry must follow creation.");
    }
    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public string TokenHash { get; private set; } = "";
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime ExpiresAtUtc { get; private set; }
    public DateTime? ConsumedAtUtc { get; private set; }
    public bool IsActive(DateTime now) => ConsumedAtUtc is null && now < ExpiresAtUtc;
    public void Invalidate(DateTime now) => ConsumedAtUtc ??= Rules.Utc(now);
}
