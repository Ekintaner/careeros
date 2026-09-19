using CareerOS.Domain.Notes;
using CareerOS.Domain.Planning;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CareerOS.Infrastructure.Persistence.Configurations;

internal sealed class InterviewNoteConfiguration : IEntityTypeConfiguration<InterviewNote>
{
    public void Configure(EntityTypeBuilder<InterviewNote> b)
    {
        b.ToTable("interview_notes");
        Mapping.Identity(b);
        Mapping.Owner(b);
        Mapping.Audit(b);
        Mapping.Text(b, nameof(InterviewNote.Body), 20000, true);
        b.HasOne<Interview>().WithMany().HasForeignKey(x => new { x.UserId, x.InterviewId }).HasPrincipalKey(x => new { x.UserId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => new { x.UserId, x.InterviewId, x.CreatedAtUtc });
    }
}
