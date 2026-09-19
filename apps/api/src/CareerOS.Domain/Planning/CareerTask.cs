using CareerOS.Domain.Applications;
using CareerOS.Domain.Common;

namespace CareerOS.Domain.Planning;

public enum TaskPriority { Low, Normal, High }

public sealed class CareerTask : IAuditedEntity
{
    private CareerTask() { }
    public CareerTask(Guid userId, string title)
    {
        Id = Guid.NewGuid();
        UserId = Rules.Id(userId, nameof(userId));
        Rename(title);
    }
    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public Guid? ApplicationId { get; private set; }
    public string Title { get; private set; } = "";
    public string? Description { get; private set; }
    public DateTime? DueAtUtc { get; private set; }
    public DateTime? CompletedAtUtc { get; private set; }
    public TaskPriority Priority { get; private set; } = TaskPriority.Normal;
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime UpdatedAtUtc { get; private set; }

    public void Rename(string title) => Title = Rules.Required(title, 200, nameof(title));
    public void Plan(string? description, DateTime? dueAtUtc, TaskPriority priority)
    {
        var validDescription = Rules.Optional(description, 10000, nameof(description));
        Rules.Utc(dueAtUtc);
        Rules.EnumValue(priority);
        Description = validDescription;
        DueAtUtc = dueAtUtc;
        Priority = priority;
    }
    public void LinkApplication(JobApplication? application)
    {
        if (application is not null) Rules.SameOwner(UserId, application.UserId);
        ApplicationId = application?.Id;
    }
    public void Complete(TimeProvider? clock = null)
    {
        if (CompletedAtUtc.HasValue) return;
        var now = (clock ?? TimeProvider.System).GetUtcNow().UtcDateTime;
        if (now < CreatedAtUtc) throw new DomainValidationException("Completion cannot precede creation.");
        CompletedAtUtc = now;
    }
    public void Reopen() => CompletedAtUtc = null;
}
