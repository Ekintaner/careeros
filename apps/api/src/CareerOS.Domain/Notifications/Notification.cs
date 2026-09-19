using CareerOS.Domain.Common;

namespace CareerOS.Domain.Notifications;

public sealed class Notification : IAuditedEntity
{
    private Notification() { }
    public Notification(Guid userId, string type, string title, string message)
    {
        Id = Guid.NewGuid();
        UserId = Rules.Id(userId, nameof(userId));
        Type = Rules.Required(type, 80, nameof(type));
        Title = Rules.Required(title, 200, nameof(title));
        Message = Rules.Required(message, 4000, nameof(message));
    }
    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public string Type { get; private set; } = "";
    public string Title { get; private set; } = "";
    public string Message { get; private set; } = "";
    public DateTime? ReadAtUtc { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime UpdatedAtUtc { get; private set; }

    public void MarkRead(TimeProvider? clock = null)
    {
        if (ReadAtUtc.HasValue) return;
        var now = (clock ?? TimeProvider.System).GetUtcNow().UtcDateTime;
        if (now < CreatedAtUtc) throw new DomainValidationException("Read time cannot precede creation.");
        ReadAtUtc = now;
    }
}
