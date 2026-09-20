using CareerOS.Domain.Common;

namespace CareerOS.Domain.Accounts;

public sealed class User : IAuditedEntity
{
    private User() { }
    public User(string email, string displayName)
    {
        Id = Guid.NewGuid();
        ChangeEmail(email);
        Rename(displayName);
    }

    public Guid Id { get; private set; }
    public string Email { get; private set; } = "";
    public string EmailNormalized { get; private set; } = "";
    public string DisplayName { get; private set; } = "";
    public string? PasswordHash { get; private set; }
    public Guid SecurityStamp { get; private set; }
    public DateTime? EmailVerifiedAtUtc { get; private set; }
    public DateTime? LastLoginAtUtc { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime UpdatedAtUtc { get; private set; }

    public void ChangeEmail(string email)
    {
        Email = Rules.Email(email);
        EmailNormalized = Rules.Normalize(Email);
    }

    public void Rename(string displayName) => DisplayName = Rules.Required(displayName, 120, nameof(displayName));

    public static string NormalizeEmail(string email) => Rules.Normalize(Rules.Email(email));

    public void SetPasswordHash(string hash)
    {
        PasswordHash = Rules.Required(hash, 1024, nameof(hash));
        SecurityStamp = Guid.NewGuid();
    }

    public void RecordLogin(DateTime now) => LastLoginAtUtc = Rules.Utc(now);
}
