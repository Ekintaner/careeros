using CareerOS.Application.Authentication;
using CareerOS.Domain.Accounts;
using CareerOS.Domain.Common;
using CareerOS.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace CareerOS.Infrastructure.Authentication;

public sealed record SessionCredentials(User User, Guid FamilyId, string RefreshSecret, DateTime ExpiresAtUtc);

// One expensive verification for both unknown accounts and incorrect passwords.
public sealed class DummyPassword(IPasswordHasher<User> hasher)
{
    public string Hash { get; } = hasher.HashPassword(new User("dummy@invalid.example", "Dummy"), SecurityTokens.Create());
}

public sealed class AuthService(CareerOSDbContext db, IPasswordHasher<User> hasher,
    DummyPassword dummy, TimeProvider clock, IPasswordResetDelivery delivery)
{
    private DateTime Now => clock.GetUtcNow().UtcDateTime;
    public async Task<SessionCredentials> Register(string email, string password, string name, int days, CancellationToken ct)
    {
        PasswordPolicy.Validate(password);
        var user = new User(email, name);
        user.SetPasswordHash(hasher.HashPassword(user, password));
        user.RecordLogin(Now);
        db.Add(user);
        db.Add(new CareerProfile(user.Id));
        var credentials = NewSession(user, days);
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateException e) when (e.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        { throw new DomainValidationException("Unable to create an account with these details. Try signing in or resetting your password."); }
        return credentials;
    }

    public async Task<SessionCredentials> Login(string email, string password, int days, CancellationToken ct)
    {
        string normalized;
        try { normalized = User.NormalizeEmail(email); }
        catch (DomainValidationException) { normalized = ""; }
        if (password is null || password.Length > 128) throw new AuthenticationFailure("Invalid email or password.");
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var user = await db.Users.FromSqlInterpolated($"SELECT *, xmin FROM users WHERE \"EmailNormalized\" = {normalized} FOR UPDATE").SingleOrDefaultAsync(ct);
        var result = hasher.VerifyHashedPassword(user ?? new User("dummy@invalid.example", "Dummy"), user?.PasswordHash ?? dummy.Hash, password);
        if (user?.PasswordHash is null || result == PasswordVerificationResult.Failed)
            throw new AuthenticationFailure("Invalid email or password.");
        if (result == PasswordVerificationResult.SuccessRehashNeeded)
        {
            user.SetPasswordHash(hasher.HashPassword(user, password));
            await RevokeAll(user.Id, ct);
        }
        user.RecordLogin(Now);
        var credentials = NewSession(user, days);
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return credentials;
    }

    public async Task<SessionCredentials> Refresh(string? secret, CancellationToken ct)
    {
        if (!SecurityTokens.IsWellFormed(secret)) throw new AuthenticationFailure();
        var hash = SecurityTokens.Hash(secret!);
        var owner = await db.RefreshTokens.AsNoTracking().Where(x => x.TokenHash == hash).Select(x => (Guid?)x.UserId).SingleOrDefaultAsync(ct);
        if (owner is null) throw new AuthenticationFailure();
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var user = await LockUser(owner.Value, ct);
        var old = await db.RefreshTokens.SingleAsync(x => x.TokenHash == hash, ct);
        if (old.ReplacedByTokenId.HasValue)
        {
            await RevokeFamily(user.Id, old.FamilyId, ct);
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
            throw new AuthenticationFailure();
        }
        if (!old.IsActive(Now) || user.PasswordHash is null) throw new AuthenticationFailure();
        var nextSecret = SecurityTokens.Create();
        var next = new RefreshToken(user.Id, old.FamilyId, SecurityTokens.Hash(nextSecret), Now, old.ExpiresAtUtc);
        old.RotateTo(next, Now);
        db.Add(next);
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return new(user, next.FamilyId, nextSecret, next.ExpiresAtUtc);
    }

    public async Task Logout(string? secret, CancellationToken ct)
    {
        if (!SecurityTokens.IsWellFormed(secret)) return;
        var hash = SecurityTokens.Hash(secret!);
        var token = await db.RefreshTokens.AsNoTracking().SingleOrDefaultAsync(x => x.TokenHash == hash, ct);
        if (token is null) return;
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await LockUser(token.UserId, ct);
        await RevokeFamily(token.UserId, token.FamilyId, ct);
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
    }

    public async Task LogoutAll(Guid userId, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await LockUser(userId, ct);
        await RevokeAll(userId, ct);
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
    }

    public async Task ChangePassword(Guid userId, string current, string replacement, CancellationToken ct)
    {
        PasswordPolicy.Validate(replacement);
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var user = await LockUser(userId, ct);
        if (current is null || current.Length > 128 || user.PasswordHash is null
            || hasher.VerifyHashedPassword(user, user.PasswordHash, current) == PasswordVerificationResult.Failed)
            throw new AuthenticationFailure("Current password is incorrect.");
        user.SetPasswordHash(hasher.HashPassword(user, replacement));
        await RevokeAll(userId, ct);
        await InvalidateResets(userId, ct);
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
    }

    public async Task ForgotPassword(string email, CancellationToken ct)
    {
        // Check delivery before lookup: unavailable delivery has the same response for every address.
        if (!delivery.IsAvailable) throw new DeliveryUnavailableException();
        string normalized;
        try { normalized = User.NormalizeEmail(email); }
        catch (DomainValidationException) { return; }
        var id = await db.Users.Where(x => x.EmailNormalized == normalized).Select(x => (Guid?)x.Id).SingleOrDefaultAsync(ct);
        if (id is null) return;
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var user = await LockUser(id.Value, ct);
        // One issuance per account per minute, independent of source IP. No account lockout.
        if (await db.PasswordResetTokens.AnyAsync(x => x.UserId == user.Id && x.CreatedAtUtc > Now.AddMinutes(-1), ct)) return;
        await InvalidateResets(user.Id, ct);
        var secret = SecurityTokens.Create();
        db.Add(new PasswordResetToken(user.Id, SecurityTokens.Hash(secret), Now, Now.AddMinutes(30)));
        await db.SaveChangesAsync(ct);
        // Adapter must throw on failure. No provider is installed in production.
        await delivery.SendAsync(user.Email, secret, ct);
        await tx.CommitAsync(ct);
    }

    public async Task ResetPassword(string? secret, string password, CancellationToken ct)
    {
        PasswordPolicy.Validate(password);
        if (!SecurityTokens.IsWellFormed(secret)) throw new DomainValidationException("Reset link is invalid or expired.");
        var hash = SecurityTokens.Hash(secret!);
        var id = await db.PasswordResetTokens.Where(x => x.TokenHash == hash).Select(x => (Guid?)x.UserId).SingleOrDefaultAsync(ct);
        if (id is null) throw new DomainValidationException("Reset link is invalid or expired.");
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var user = await LockUser(id.Value, ct);
        var token = await db.PasswordResetTokens.SingleAsync(x => x.TokenHash == hash, ct);
        if (!token.IsActive(Now)) throw new DomainValidationException("Reset link is invalid or expired.");
        user.SetPasswordHash(hasher.HashPassword(user, password));
        await InvalidateResets(user.Id, ct);
        await RevokeAll(user.Id, ct);
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
    }

    private Task<User> LockUser(Guid id, CancellationToken ct) =>
        db.Users.FromSqlInterpolated($"SELECT *, xmin FROM users WHERE \"Id\" = {id} FOR UPDATE").SingleAsync(ct);
    private SessionCredentials NewSession(User user, int days)
    {
        var secret = SecurityTokens.Create();
        var token = new RefreshToken(user.Id, Guid.NewGuid(), SecurityTokens.Hash(secret), Now, Now.AddDays(days));
        db.Add(token);
        return new(user, token.FamilyId, secret, token.ExpiresAtUtc);
    }
    private async Task RevokeFamily(Guid user, Guid family, CancellationToken ct)
    {
        foreach (var token in await db.RefreshTokens.Where(x => x.UserId == user && x.FamilyId == family && x.RevokedAtUtc == null).ToListAsync(ct)) token.Revoke(Now);
    }
    private async Task RevokeAll(Guid user, CancellationToken ct)
    {
        foreach (var token in await db.RefreshTokens.Where(x => x.UserId == user && x.RevokedAtUtc == null).ToListAsync(ct)) token.Revoke(Now);
    }
    private async Task InvalidateResets(Guid user, CancellationToken ct)
    {
        foreach (var token in await db.PasswordResetTokens.Where(x => x.UserId == user && x.ConsumedAtUtc == null).ToListAsync(ct)) token.Invalidate(Now);
    }
}
