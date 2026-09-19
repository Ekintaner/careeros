using CareerOS.Domain.Documents;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CareerOS.Infrastructure.Persistence.Configurations;

internal sealed class ResumeConfiguration : IEntityTypeConfiguration<Resume>
{
    public void Configure(EntityTypeBuilder<Resume> b)
    {
        b.ToTable("resumes");
        Mapping.Identity(b);
        Mapping.Owner(b, true);
        Mapping.Audit(b);
        Mapping.Text(b, nameof(Resume.Name), 200, true);
        Mapping.Enum<Resume, DocumentType>(b, nameof(Resume.DocumentType));
        Mapping.Check(b, "resume_document", "\"DocumentType\" = 'Resume'");
        b.HasOne<Document>().WithMany().HasForeignKey(x => new { x.UserId, x.DocumentId, x.DocumentType }).HasPrincipalKey(x => new { x.UserId, x.Id, x.Type }).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => x.DocumentId).IsUnique();
        b.HasIndex(x => x.UserId).IsUnique().HasFilter("\"IsPrimary\" = TRUE");
        b.HasIndex(x => new { x.UserId, x.CreatedAtUtc });
    }
}
