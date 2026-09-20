using CareerOS.Domain.Accounts;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CareerOS.Infrastructure.Persistence.Configurations;

internal sealed class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
{
    public void Configure(EntityTypeBuilder<RefreshToken> b)
    {
        b.ToTable("refresh_tokens");
        Mapping.Identity(b);
        Mapping.Owner(b);
        b.HasAlternateKey(x => new { x.UserId, x.FamilyId, x.Id });
        b.Property(x => x.TokenHash).HasMaxLength(64).IsRequired();
        b.HasIndex(x => x.TokenHash).IsUnique();
        b.HasIndex(x => new { x.UserId, x.FamilyId });
        b.HasIndex(x => x.ExpiresAtUtc);
        b.HasOne<RefreshToken>().WithMany().HasForeignKey(x => new { x.UserId, x.FamilyId, x.ReplacedByTokenId })
            .HasPrincipalKey(x => new { x.UserId, x.FamilyId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        Mapping.Check(b, "expiry", "\"ExpiresAtUtc\" > \"CreatedAtUtc\"");
        Mapping.Check(b, "revocation", "\"RevokedAtUtc\" >= \"CreatedAtUtc\"");
        Mapping.Check(b, "replacement", "\"ReplacedByTokenId\" IS NULL OR (\"RevokedAtUtc\" IS NOT NULL AND \"ReplacedByTokenId\" <> \"Id\")");
        Mapping.Check(b, "hash", "\"TokenHash\" ~ '^[A-F0-9]{64}$'");
    }
}
internal sealed class PasswordResetTokenConfiguration : IEntityTypeConfiguration<PasswordResetToken>
{
    public void Configure(EntityTypeBuilder<PasswordResetToken> b)
    {
        b.ToTable("password_reset_tokens");
        Mapping.Identity(b);
        Mapping.Owner(b);
        b.Property(x => x.TokenHash).HasMaxLength(64).IsRequired();
        b.HasIndex(x => x.TokenHash).IsUnique();
        b.HasIndex(x => x.ExpiresAtUtc);
        Mapping.Check(b, "expiry", "\"ExpiresAtUtc\" > \"CreatedAtUtc\"");
        Mapping.Check(b, "consumption", "\"ConsumedAtUtc\" >= \"CreatedAtUtc\"");
        Mapping.Check(b, "hash", "\"TokenHash\" ~ '^[A-F0-9]{64}$'");
    }
}
