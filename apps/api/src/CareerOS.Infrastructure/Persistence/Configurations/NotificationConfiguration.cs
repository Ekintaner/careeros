using CareerOS.Domain.Notifications;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CareerOS.Infrastructure.Persistence.Configurations;

internal sealed class NotificationConfiguration : IEntityTypeConfiguration<Notification>
{
    public void Configure(EntityTypeBuilder<Notification> b)
    {
        b.ToTable("notifications");
        Mapping.Identity(b);
        Mapping.Owner(b);
        Mapping.Audit(b);
        Mapping.Text(b, nameof(Notification.Type), 80, true);
        Mapping.Text(b, nameof(Notification.Title), 200, true);
        Mapping.Text(b, nameof(Notification.Message), 4000, true);
        b.Property(x => x.ReadAtUtc).HasColumnType("timestamp with time zone");
        Mapping.Check(b, "read_order", "\"ReadAtUtc\" >= \"CreatedAtUtc\"");
        b.HasIndex(x => new { x.UserId, x.CreatedAtUtc });
        b.HasIndex(x => new { x.UserId, x.CreatedAtUtc }, "IX_notifications_unread").HasFilter("\"ReadAtUtc\" IS NULL");
    }
}
