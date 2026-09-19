using CareerOS.Domain.Documents;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CareerOS.Infrastructure.Persistence.Configurations;

internal sealed class DocumentConfiguration : IEntityTypeConfiguration<Document>
{
    public void Configure(EntityTypeBuilder<Document> b)
    {
        b.ToTable("documents");
        Mapping.Identity(b);
        Mapping.Owner(b);
        Mapping.Audit(b);
        b.HasAlternateKey(x => new { x.UserId, x.Id, x.Type });
        Mapping.Text(b, nameof(Document.Name), 200, true);
        Mapping.Text(b, nameof(Document.OriginalFileName), 255, true);
        Mapping.Text(b, nameof(Document.StorageKey), 512, true);
        Mapping.Text(b, nameof(Document.ContentType), 127, true);
        b.Property(x => x.FileSizeBytes).IsRequired();
        Mapping.Enum<Document, DocumentType>(b, nameof(Document.Type));
        Mapping.Check(b, "file_size", "\"FileSizeBytes\" >= 0");
        b.HasIndex(x => x.StorageKey).IsUnique();
        b.HasIndex(x => new { x.UserId, x.CreatedAtUtc });
    }
}
