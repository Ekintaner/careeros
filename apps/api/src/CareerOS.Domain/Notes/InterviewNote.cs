using CareerOS.Domain.Common;
using CareerOS.Domain.Planning;

namespace CareerOS.Domain.Notes;

public sealed class InterviewNote : IAuditedEntity
{
    private InterviewNote() { }
    public InterviewNote(Interview interview, string body)
    {
        Id = Guid.NewGuid();
        UserId = interview.UserId;
        InterviewId = interview.Id;
        Edit(body);
    }
    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public Guid InterviewId { get; private set; }
    public string Body { get; private set; } = "";
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime UpdatedAtUtc { get; private set; }
    public void Edit(string body) => Body = Rules.Required(body, 20000, nameof(body));
}
