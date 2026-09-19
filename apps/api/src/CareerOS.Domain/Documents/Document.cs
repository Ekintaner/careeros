using CareerOS.Domain.Common;

namespace CareerOS.Domain.Documents;

public enum DocumentType { Resume, CoverLetter, Certificate, Portfolio, Other }

public sealed class Document : IAuditedEntity
{
    private Document() { }
    public Document(Guid userId, string name, string originalFileName, string storageKey, string contentType, long fileSizeBytes, DocumentType type)
    {
        Id = Guid.NewGuid();
        UserId = Rules.Id(userId, nameof(userId));
        Rename(name);
        OriginalFileName = Rules.Required(originalFileName, 255, nameof(originalFileName));
        StorageKey = Rules.Required(storageKey, 512, nameof(storageKey));
        ContentType = Rules.Required(contentType, 127, nameof(contentType));
        if (fileSizeBytes < 0) throw new DomainValidationException("File size cannot be negative.");
        FileSizeBytes = fileSizeBytes;
        Type = Rules.EnumValue(type);
    }
    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public string Name { get; private set; } = "";
    public string OriginalFileName { get; private set; } = "";
    public string StorageKey { get; private set; } = "";
    public string ContentType { get; private set; } = "";
    public long FileSizeBytes { get; private set; }
    public DocumentType Type { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime UpdatedAtUtc { get; private set; }

    public void Rename(string name) => Name = Rules.Required(name, 200, nameof(name));
}
