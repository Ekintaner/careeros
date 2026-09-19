using CareerOS.Domain.Common;

namespace CareerOS.Domain.Applications;

public sealed class ApplicationStatusHistory
{
    private ApplicationStatusHistory() { }
    internal ApplicationStatusHistory(JobApplication application, ApplicationStatus? from, ApplicationStatus to, int sequence, DateTime changedAtUtc)
    {
        Id = Guid.NewGuid();
        UserId = application.UserId;
        ApplicationId = application.Id;
        FromStatus = from;
        ToStatus = to;
        Sequence = sequence;
        ChangedAtUtc = Rules.Utc(changedAtUtc);
    }
    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public Guid ApplicationId { get; private set; }
    public ApplicationStatus? FromStatus { get; private set; }
    public ApplicationStatus ToStatus { get; private set; }
    public int Sequence { get; private set; }
    public DateTime ChangedAtUtc { get; private set; }
}
