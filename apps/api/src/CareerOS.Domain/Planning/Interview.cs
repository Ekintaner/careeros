using CareerOS.Domain.Applications;
using CareerOS.Domain.Common;

namespace CareerOS.Domain.Planning;

public enum InterviewType { Recruiter, HR, Technical, Behavioral, HiringManager, CaseStudy, Final, Other }

public sealed class Interview : IAuditedEntity
{
    private Interview() { }
    public Interview(JobApplication application, InterviewType type, DateTime startUtc, DateTime endUtc)
    {
        Id = Guid.NewGuid();
        UserId = application.UserId;
        ApplicationId = application.Id;
        Type = Rules.EnumValue(type);
        Reschedule(startUtc, endUtc);
    }
    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public Guid ApplicationId { get; private set; }
    public InterviewType Type { get; private set; }
    public DateTime ScheduledStartUtc { get; private set; }
    public DateTime ScheduledEndUtc { get; private set; }
    public string? Location { get; private set; }
    public string? MeetingUrl { get; private set; }
    public string? Outcome { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime UpdatedAtUtc { get; private set; }

    public void ChangeType(InterviewType type) => Type = Rules.EnumValue(type);

    public void Reschedule(DateTime startUtc, DateTime endUtc)
    {
        Rules.Utc(startUtc);
        Rules.Utc(endUtc);
        if (endUtc <= startUtc) throw new DomainValidationException("Interview end must be after start.");
        ScheduledStartUtc = startUtc;
        ScheduledEndUtc = endUtc;
    }

    public void SetVenue(string? location, string? meetingUrl)
    {
        var validLocation = Rules.Optional(location, 300, nameof(location));
        var validUrl = Rules.Url(meetingUrl);
        Location = validLocation;
        MeetingUrl = validUrl;
    }
    public void RecordOutcome(string? outcome) => Outcome = Rules.Optional(outcome, 2000, nameof(outcome));
}
