using System.Text.Json.Serialization;
using CareerOS.Application.Opportunities;
using CareerOS.Domain.Applications;
using CareerOS.Domain.Common;

namespace CareerOS.Application.Applications;

public sealed class ApplicationNotFoundException : Exception;
public sealed class ApplicationConflictException(string message, Guid? existingId = null) : Exception(message)
{ public Guid? ExistingId { get; } = existingId; }

public sealed record ApplicationQuery(int Page = 1, int PageSize = 20, string? Search = null, string? Status = null,
    Guid? CompanyId = null, Guid? JobId = null, string? State = null, string? Sort = null, DateTime? AppliedFrom = null, DateTime? AppliedTo = null)
{
    public void Validate()
    {
        if (Page is < 1 or > 10000 || PageSize is < 1 or > 100) throw new DomainValidationException("Use page 1–10,000 and page size 1–100.");
        OpportunityValidation.Text(Search);
        OpportunityValidation.EnumValue<ApplicationStatus>(Status);
        if (State is not (null or "active" or "archived" or "all")) throw new DomainValidationException("Choose active, archived or all applications.");
        if (Sort is not (null or "newest" or "oldest" or "updated" or "applied" or "company" or "title")) throw new DomainValidationException("Choose a supported sort order.");
        if (AppliedFrom?.Kind is not (null or DateTimeKind.Utc) || AppliedTo?.Kind is not (null or DateTimeKind.Utc) || AppliedFrom > AppliedTo)
            throw new DomainValidationException("Use an ordered UTC applied-date range.");
    }
}
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record CreateApplicationInput(Guid JobId, string InitialStatus = "Saved", Guid? ResumeId = null, string? Source = null, DateTime? AppliedAtUtc = null);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record EditApplicationInput(uint Version, Guid? ResumeId = null, string? Source = null, DateTime? AppliedAtUtc = null);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record StatusInput(string Status, uint Version);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record VersionInput(uint Version);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record NoteInput(string Body, uint Version = 0);
public sealed record ApplicationCard(Guid Id, Guid JobId, Guid CompanyId, string JobTitle, string CompanyName, string? Location,
    string Status, Guid? ResumeId, DateTime? AppliedAtUtc, DateTime CreatedAtUtc, DateTime UpdatedAtUtc, uint Version);
public sealed record ApplicationView(Guid Id, Guid JobId, Guid CompanyId, string JobTitle, string CompanyName, string? Location,
    string Status, Guid? ResumeId, string? ResumeName, string? Source, string? SourceUrl, DateTime? AppliedAtUtc,
    DateTime CreatedAtUtc, DateTime UpdatedAtUtc, DateTime LastStatusChangedAtUtc, uint Version);
public sealed record HistoryView(Guid Id, string? FromStatus, string ToStatus, int Sequence, DateTime ChangedAtUtc);
public sealed record NoteView(Guid Id, string Body, DateTime CreatedAtUtc, DateTime UpdatedAtUtc, uint Version);
public sealed record ResumeChoice(Guid Id, string Name);
public sealed record StageCount(string Status, int Count);
