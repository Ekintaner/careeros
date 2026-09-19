using CareerOS.Domain.Accounts;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CareerOS.Infrastructure.Persistence.Configurations;

internal sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> b)
    {
        Mapping.Identity(b);
        b.ToTable("users");
        Mapping.Audit(b);
        Mapping.Text(b, nameof(User.Email), 254, true);
        Mapping.Text(b, nameof(User.DisplayName), 120, true);
        b.Property(x => x.EmailNormalized).HasMaxLength(254).HasComputedColumnSql("translate(btrim(\"Email\"), 'ABCDEFGHIJKLMNOPQRSTUVWXYZ', 'abcdefghijklmnopqrstuvwxyz')", stored: true);
        b.HasIndex(x => x.EmailNormalized).IsUnique();
        Mapping.Check(b, "email_shape", "\"Email\" = btrim(\"Email\") AND \"Email\" ~ '^[!-~]+@[!-~]+$'");
    }
}
