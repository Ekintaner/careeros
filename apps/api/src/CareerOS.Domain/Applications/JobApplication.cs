using CareerOS.Domain.Common;
using CareerOS.Domain.Documents;
using CareerOS.Domain.Opportunities;

namespace CareerOS.Domain.Applications;

public sealed class JobApplication : IAuditedEntity
{
    private readonly List<ApplicationStatusHistory> _statusHistory = [];
    private JobApplication() { }
    public JobApplication(Job job, TimeProvider? clock = null, ApplicationStatus initialStatus = ApplicationStatus.Saved)
    {
        if (initialStatus is not (ApplicationStatus.Saved or ApplicationStatus.Applied))
            throw new DomainValidationException("Create an application as Saved or Applied.");
        Id = Guid.NewGuid();
        UserId = job.UserId;
        JobId = job.Id;
        Status = initialStatus;
        LastStatusChangedAtUtc = (clock ?? TimeProvider.System).GetUtcNow().UtcDateTime;
        if (Status == ApplicationStatus.Applied) AppliedAtUtc = LastStatusChangedAtUtc;
        _statusHistory.Add(new ApplicationStatusHistory(this, null, Status, 0, LastStatusChangedAtUtc));
    }
    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public Guid JobId { get; private set; }
    public Guid? ResumeId { get; private set; }
    public ApplicationStatus Status { get; private set; }
    public int StatusRevision { get; private set; }
    public DateTime LastStatusChangedAtUtc { get; private set; }
    public DateTime? AppliedAtUtc { get; private set; }
    public string? Source { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime UpdatedAtUtc { get; private set; }
    public IReadOnlyCollection<ApplicationStatusHistory> StatusHistory => _statusHistory.AsReadOnly();

    public void AttachResume(Resume? resume)
    {
        if (resume is not null) Rules.SameOwner(UserId, resume.UserId);
        ResumeId = resume?.Id;
    }

    public void SetSource(string? source) => Source = Rules.Optional(source, 120, nameof(source));

    public void CorrectAppliedDate(DateTime? date)
    {
        Rules.Utc(date);
        if (date.HasValue && (!AppliedAtUtc.HasValue || date.Value.Year < 1900 || date > LastStatusChangedAtUtc))
            throw new DomainValidationException("Applied date must be from 1900 through the last status change, after the application has been marked Applied.");
        if (!date.HasValue && AppliedAtUtc.HasValue)
            throw new DomainValidationException("The applied date cannot be cleared after applying.");
        AppliedAtUtc = date;
    }

    public void Restore(ApplicationStatusHistory lastEvent, TimeProvider? clock = null)
    {
        if (Status != ApplicationStatus.Archived || lastEvent.ApplicationId != Id || lastEvent.UserId != UserId
            || lastEvent.Sequence != StatusRevision || lastEvent.ToStatus != ApplicationStatus.Archived
            || lastEvent.FromStatus is null or ApplicationStatus.Archived)
            throw new DomainValidationException("Only an archived application with a valid previous stage can be restored.");
        AppendTransition(lastEvent.FromStatus.Value, clock);
    }

    public void TransitionTo(ApplicationStatus next, TimeProvider? clock = null)
    {
        Rules.EnumValue(next);
        var allowed = Status switch
        {
            ApplicationStatus.Saved => next is ApplicationStatus.Applied or ApplicationStatus.Withdrawn or ApplicationStatus.Archived,
            ApplicationStatus.Applied => next is ApplicationStatus.Assessment or ApplicationStatus.Interview or ApplicationStatus.Offer or ApplicationStatus.Rejected or ApplicationStatus.Withdrawn or ApplicationStatus.Archived,
            ApplicationStatus.Assessment => next is ApplicationStatus.Applied or ApplicationStatus.Interview or ApplicationStatus.Offer or ApplicationStatus.Rejected or ApplicationStatus.Withdrawn or ApplicationStatus.Archived,
            ApplicationStatus.Interview => next is ApplicationStatus.Applied or ApplicationStatus.Assessment or ApplicationStatus.Offer or ApplicationStatus.Rejected or ApplicationStatus.Withdrawn or ApplicationStatus.Archived,
            ApplicationStatus.Offer => next is ApplicationStatus.Applied or ApplicationStatus.Assessment or ApplicationStatus.Interview or ApplicationStatus.Accepted or ApplicationStatus.Rejected or ApplicationStatus.Withdrawn or ApplicationStatus.Archived,
            ApplicationStatus.Accepted or ApplicationStatus.Rejected or ApplicationStatus.Withdrawn => next is ApplicationStatus.Archived,
            _ => false
        };
        if (!allowed) throw new DomainValidationException($"Transition from {Status} to {next} is not supported.");
        AppendTransition(next, clock);
    }

    private void AppendTransition(ApplicationStatus next, TimeProvider? clock)
    {
        var changedAt = (clock ?? TimeProvider.System).GetUtcNow().UtcDateTime;
        if (changedAt < LastStatusChangedAtUtc) throw new DomainValidationException("Status history cannot move backwards in time.");
        var revision = checked(StatusRevision + 1);
        _statusHistory.Add(new ApplicationStatusHistory(this, Status, next, revision, changedAt));
        Status = next;
        StatusRevision = revision;
        LastStatusChangedAtUtc = changedAt;
        if (next == ApplicationStatus.Applied) AppliedAtUtc ??= changedAt;
    }
}
