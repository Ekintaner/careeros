using CareerOS.Domain.Applications;
using CareerOS.Domain.Notes;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CareerOS.Infrastructure.Persistence.Configurations;

internal sealed class ApplicationNoteConfiguration : IEntityTypeConfiguration<ApplicationNote>
{
    public void Configure(EntityTypeBuilder<ApplicationNote> b)
    {
        b.ToTable("application_notes");
        Mapping.Identity(b);
        Mapping.Owner(b);
        Mapping.Audit(b);
        Mapping.Text(b, nameof(ApplicationNote.Body), 20000, true);
        b.HasOne<JobApplication>().WithMany().HasForeignKey(x => new { x.UserId, x.ApplicationId }).HasPrincipalKey(x => new { x.UserId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => new { x.UserId, x.ApplicationId, x.CreatedAtUtc });
    }
}
