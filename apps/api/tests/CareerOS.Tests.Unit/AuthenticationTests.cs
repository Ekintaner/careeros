using CareerOS.Api.Authentication;
using CareerOS.Application.Authentication;
using CareerOS.Domain.Accounts;
using CareerOS.Domain.Common;
using CareerOS.Infrastructure.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using Xunit;

namespace CareerOS.Tests.Unit;

public sealed class AuthenticationTests
{
    private static readonly DateTime Now = new(2026, 9, 20, 0, 0, 0, DateTimeKind.Utc);
    [Fact]
    public void RotationRevokesOldTokenAndPreservesAbsoluteExpiry()
    {
        var token = new RefreshToken(Guid.NewGuid(), Guid.NewGuid(), SecurityTokens.Hash(SecurityTokens.Create()), Now, Now.AddDays(30));
        var next = new RefreshToken(token.UserId, token.FamilyId, SecurityTokens.Hash(SecurityTokens.Create()), Now.AddMinutes(1), token.ExpiresAtUtc);
        token.RotateTo(next, Now.AddMinutes(1));
        Assert.False(token.IsActive(Now.AddMinutes(1)));
        Assert.Equal(next.Id, token.ReplacedByTokenId);
        Assert.Throws<DomainValidationException>(() => token.RotateTo(next, Now.AddMinutes(2)));
        Assert.False(next.IsActive(next.ExpiresAtUtc));
    }
    [Fact]
    public void CannotRotateIntoAnotherOwnerOrExtendLifetime()
    {
        var token = new RefreshToken(Guid.NewGuid(), Guid.NewGuid(), "hash", Now, Now.AddDays(1));
        Assert.Throws<DomainValidationException>(() => token.RotateTo(new RefreshToken(Guid.NewGuid(), token.FamilyId, "hash", Now, token.ExpiresAtUtc), Now));
        Assert.Throws<DomainValidationException>(() => token.RotateTo(new RefreshToken(token.UserId, token.FamilyId, "hash", Now, Now.AddDays(2)), Now));
    }
    [Fact]
    public void RevocationIsIdempotentAndResetExpires()
    {
        var token = new PasswordResetToken(Guid.NewGuid(), "hash", Now, Now.AddMinutes(30));
        Assert.False(token.IsActive(Now.AddMinutes(30)));
        token.Invalidate(Now.AddMinutes(1)); token.Invalidate(Now.AddMinutes(2));
        Assert.Equal(Now.AddMinutes(1), token.ConsumedAtUtc);
        Assert.False(token.IsActive(Now.AddMinutes(1)));
    }
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("short password")]
    [InlineData("aaaaaaaaaaaaaaaa")]
    [InlineData("valid length but\ncontrol")]
    public void InvalidPasswordsAreRejected(string? password) => Assert.Throws<DomainValidationException>(() => PasswordPolicy.Validate(password));
    [Fact] public void LongPassphrasesNeedNoCharacterClasses() => PasswordPolicy.Validate("several ordinary memorable words");
    [Fact]
    public void FrameworkHashIsSaltedAndVerifiable()
    {
        var user = new User("a@example.com", "Account");
        var hasher = new PasswordHasher<User>(Options.Create(new PasswordHasherOptions { IterationCount = 210_000 }));
        var one = hasher.HashPassword(user, "several ordinary memorable words");
        Assert.NotEqual(one, hasher.HashPassword(user, "several ordinary memorable words"));
        Assert.Equal(PasswordVerificationResult.Success, hasher.VerifyHashedPassword(user, one, "several ordinary memorable words"));
        Assert.Equal(PasswordVerificationResult.Failed, hasher.VerifyHashedPassword(user, one, "incorrect"));
        user.SetPasswordHash(one); var stamp = user.SecurityStamp;
        user.SetPasswordHash(one); Assert.NotEqual(stamp, user.SecurityStamp);
    }
    [Fact]
    public void LookupAndDomainUseIdenticalNormalization()
    {
        const string email = " Person@EXAMPLE.com ";
        Assert.Equal(new User(email, "Person").EmailNormalized, User.NormalizeEmail(email));
        Assert.Equal("person@example.com", User.NormalizeEmail(email));
        Assert.Throws<DomainValidationException>(() => User.NormalizeEmail("é@example.com"));
    }
    [Fact]
    public void TokensAreRandomBoundedAndHashesDoNotRevealSecrets()
    {
        var tokens = Enumerable.Range(0, 100).Select(_ => SecurityTokens.Create()).ToArray();
        Assert.Equal(100, tokens.Distinct().Count());
        Assert.All(tokens, token => { Assert.True(SecurityTokens.IsWellFormed(token)); Assert.NotEqual(token, SecurityTokens.Hash(token)); });
        Assert.False(SecurityTokens.IsWellFormed(new string('z', 64)));
    }
    [Fact]
    public void ConfigurationRejectsMissingSecretsAndProductionCapture()
    {
        Assert.Throws<InvalidOperationException>(() => new AuthOptions().Validate(false));
        var options = new AuthOptions { Issuer = "test", Audience = "test", SigningKey = Convert.ToBase64String(System.Security.Cryptography.RandomNumberGenerator.GetBytes(32)), DevelopmentCapture = true };
        Assert.Throws<InvalidOperationException>(() => options.Validate(false));
        options.Validate(true);
    }
}
