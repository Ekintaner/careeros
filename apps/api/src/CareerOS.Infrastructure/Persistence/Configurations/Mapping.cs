using CareerOS.Domain.Accounts;
using CareerOS.Domain.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CareerOS.Infrastructure.Persistence.Configurations;

internal static class Mapping
{
    internal static void Identity<T>(EntityTypeBuilder<T> b) where T : class
    {
        b.HasKey("Id");
        b.Property<Guid>("Id").ValueGeneratedNever();
    }

    internal static void Owner<T>(EntityTypeBuilder<T> b, bool alternateKey = false) where T : class
    {
        b.Property<Guid>("UserId").IsRequired();
        b.HasOne<User>().WithMany().HasForeignKey("UserId").OnDelete(DeleteBehavior.Restrict);
        if (alternateKey) b.HasAlternateKey("UserId", "Id");
    }

    internal static void Audit<T>(EntityTypeBuilder<T> b) where T : class, ICreatedEntity
    {
        b.Property(x => x.CreatedAtUtc).HasColumnType("timestamp with time zone").HasDefaultValueSql("CURRENT_TIMESTAMP").IsRequired();
        if (typeof(IAuditedEntity).IsAssignableFrom(typeof(T)))
        {
            b.Property<DateTime>("UpdatedAtUtc").HasColumnType("timestamp with time zone").HasDefaultValueSql("CURRENT_TIMESTAMP").IsRequired();
            b.Property<uint>("Version").IsRowVersion();
            Check(b, "audit_order", "\"UpdatedAtUtc\" >= \"CreatedAtUtc\"");
        }
    }

    internal static void Text<T>(EntityTypeBuilder<T> b, string name, int length, bool required = false) where T : class
    {
        b.Property<string>(name).HasMaxLength(length).IsRequired(required);
        if (required) Check(b, name + "_not_blank", $"length(btrim(\"{name}\")) > 0");
    }

    internal static void Enum<T, TEnum>(EntityTypeBuilder<T> b, string property, bool nullable = false)
        where T : class where TEnum : struct, Enum
    {
        b.Property(property).HasConversion<string>().HasMaxLength(32).IsRequired(!nullable);
        var values = string.Join(", ", System.Enum.GetNames<TEnum>().Select(name => $"'{name}'"));
        Check(b, property + "_values", $"\"{property}\" IN ({values})");
    }

    internal static void Check<T>(EntityTypeBuilder<T> b, string name, string sql) where T : class =>
        b.ToTable(table => table.HasCheckConstraint($"CK_{typeof(T).Name}_{name}", sql));

    internal static void Url<T>(EntityTypeBuilder<T> b, string property) where T : class
    {
        Text(b, property, 2048);
        Check(b, property + "_http", $"\"{property}\" IS NULL OR \"{property}\" ~* '^https?://[^/@[:space:]]+([/?#][^[:space:]]*)?$'");
    }
}
