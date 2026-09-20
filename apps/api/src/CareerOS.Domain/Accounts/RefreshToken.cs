using CareerOS.Domain.Common;

namespace CareerOS.Domain.Accounts;

public sealed class RefreshToken
{
    private RefreshToken() { }
    public RefreshToken(Guid userId, Guid familyId, string tokenHash, DateTime now, DateTime expires)
    {
        Id = Guid.NewGuid();
        UserId = Rules.Id(userId, nameof(userId));
        FamilyId = Rules.Id(familyId, nameof(familyId));
        TokenHash = Rules.Required(tokenHash, 64, nameof(tokenHash));
        CreatedAtUtc = Rules.Utc(now);
        ExpiresAtUtc = Rules.Utc(expires);
        if (expires <= now) throw new DomainValidationException("Token expiry must follow creation.");
    }
    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public Guid FamilyId { get; private set; }
    public string TokenHash { get; private set; } = "";
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime ExpiresAtUtc { get; private set; }
    public DateTime? RevokedAtUtc { get; private set; }
    public Guid? ReplacedByTokenId { get; private set; }
    public bool IsActive(DateTime now) => RevokedAtUtc is null && now < ExpiresAtUtc;
    public void Revoke(DateTime now) => RevokedAtUtc ??= Rules.Utc(now);
    public void RotateTo(RefreshToken replacement, DateTime now)
    {
        if (!IsActive(now) || replacement.UserId != UserId || replacement.FamilyId != FamilyId
            || replacement.ExpiresAtUtc != ExpiresAtUtc)
            throw new DomainValidationException("Invalid token rotation.");
        Revoke(now);
        ReplacedByTokenId = replacement.Id;
    }
}
