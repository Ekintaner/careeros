using CareerOS.Domain.Applications;
using CareerOS.Domain.Common;

namespace CareerOS.Domain.Notes;

public sealed class ApplicationNote : IAuditedEntity
{
    private ApplicationNote() { }
    public ApplicationNote(JobApplication application, string body)
    {
        Id = Guid.NewGuid();
        UserId = application.UserId;
        ApplicationId = application.Id;
        Edit(body);
    }
    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public Guid ApplicationId { get; private set; }
    public string Body { get; private set; } = "";
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime UpdatedAtUtc { get; private set; }
    public void Edit(string body) => Body = Rules.Required(body, 20000, nameof(body));
}
