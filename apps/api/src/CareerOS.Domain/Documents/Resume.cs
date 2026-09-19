using CareerOS.Domain.Common;

namespace CareerOS.Domain.Documents;

public sealed class Resume : IAuditedEntity
{
    private Resume() { }
    public Resume(Document document, string name)
    {
        if (document.Type != DocumentType.Resume) throw new DomainValidationException("A resume must reference a resume document.");
        Id = Guid.NewGuid();
        UserId = document.UserId;
        DocumentId = document.Id;
        Rename(name);
    }
    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public Guid DocumentId { get; private set; }
    public DocumentType DocumentType { get; private set; } = DocumentType.Resume;
    public string Name { get; private set; } = "";
    public bool IsPrimary { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime UpdatedAtUtc { get; private set; }

    public void Rename(string name) => Name = Rules.Required(name, 200, nameof(name));
    public void SetPrimary(bool isPrimary) => IsPrimary = isPrimary;
}
