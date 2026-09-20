using CareerOS.Domain.Accounts;
using CareerOS.Domain.Common;

namespace CareerOS.Application.Authentication;

public interface ICurrentUser { Guid UserId { get; } }
public interface IPasswordResetDelivery
{
    bool IsAvailable { get; }
    Task SendAsync(string email, string token, CancellationToken cancellationToken);
}
public sealed record AccountView(Guid Id, string Email, string DisplayName, bool EmailVerified, DateTime CreatedAtUtc)
{
    public static AccountView From(User user) => new(user.Id, user.Email, user.DisplayName, user.EmailVerifiedAtUtc.HasValue, user.CreatedAtUtc);
}
public static class PasswordPolicy
{
    public static void Validate(string? password)
    {
        if (password is null || password.Length is < 15 or > 128 || string.IsNullOrWhiteSpace(password)
            || password.Any(char.IsControl) || password.Distinct().Count() == 1)
            throw new DomainValidationException("Use 15–128 characters, without control characters or a single repeated character.");
    }
}
public sealed class AuthenticationFailure(string message = "Authentication failed.") : Exception(message);
public sealed class DeliveryUnavailableException : Exception;
