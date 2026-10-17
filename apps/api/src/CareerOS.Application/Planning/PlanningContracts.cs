using System.Text.Json.Serialization;
using CareerOS.Domain.Common;
using CareerOS.Domain.Planning;
using CareerOS.Application.Opportunities;
namespace CareerOS.Application.Planning;

public sealed record PlanningQuery(int Page = 1, int PageSize = 20, Guid? ApplicationId = null, string? View = null)
{
    public void Validate(bool tasks)
    {
        if (Page is < 1 or > 10000 || PageSize is < 1 or > 100) throw new DomainValidationException("Use page 1–10,000 and page size 1–100.");
        if (tasks ? View is not (null or "open" or "completed" or "overdue" or "upcoming" or "all") : View is not (null or "upcoming" or "past" or "all"))
            throw new DomainValidationException("Choose a supported view.");
    }
}
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record InterviewInput(Guid ApplicationId, string Type, DateTime ScheduledStartUtc, DateTime ScheduledEndUtc, string? Location = null, string? MeetingUrl = null, string? Outcome = null, uint Version = 0);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record TaskInput(string Title, string? Description = null, DateTime? DueAtUtc = null, string Priority = "Normal", bool Completed = false, Guid? ApplicationId = null, uint Version = 0);
public sealed record InterviewView(Guid Id, Guid ApplicationId, string Type, DateTime ScheduledStartUtc, DateTime ScheduledEndUtc, string? Location, string? MeetingUrl, string? Outcome, DateTime CreatedAtUtc, DateTime UpdatedAtUtc, uint Version);
public sealed record TaskView(Guid Id, Guid? ApplicationId, string Title, string? Description, DateTime? DueAtUtc, string Priority, DateTime? CompletedAtUtc, DateTime CreatedAtUtc, DateTime UpdatedAtUtc, uint Version);
