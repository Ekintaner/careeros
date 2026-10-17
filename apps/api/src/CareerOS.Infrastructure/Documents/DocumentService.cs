using CareerOS.Application.Authentication;
using CareerOS.Application.Opportunities;
using CareerOS.Domain.Applications;
using CareerOS.Domain.Common;
using CareerOS.Domain.Documents;
using CareerOS.Domain.Skills;
using CareerOS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace CareerOS.Infrastructure.Documents;

public sealed record DocumentView(Guid Id, string Name, string OriginalFileName, string ContentType, long FileSizeBytes, string Type,
    DateTime CreatedAtUtc, Guid? ResumeId, bool IsPrimary, int ApplicationCount);

public sealed class DocumentService(CareerOSDbContext db, OwnedData owned, ICurrentUser current, LocalDocumentStore store, ILogger<DocumentService> logger)
{
    private async Task Lock(CancellationToken ct) => await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(hashtextextended({current.UserId.ToString()}, 7))", ct);
    private async Task<Document> Find(Guid id, CancellationToken ct) => await owned.Query<Document>().SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw new DocumentMissingException();
    private sealed class Row
    {
        public required Document Document { get; init; }
        public Resume? Resume { get; init; }
    }
    private IQueryable<Row> Rows() => from d in owned.Query<Document>().AsNoTracking()
        join r in owned.Query<Resume>().AsNoTracking() on d.Id equals r.DocumentId into resumes
        from r in resumes.DefaultIfEmpty()
        select new Row { Document = d, Resume = r };
    private IQueryable<DocumentView> Views(IQueryable<Row> rows)
    {
        var applications = owned.Query<JobApplication>();
        return rows.Select(x => new DocumentView(x.Document.Id, x.Document.Name, x.Document.OriginalFileName,
            x.Document.ContentType, x.Document.FileSizeBytes, x.Document.Type.ToString(), x.Document.CreatedAtUtc,
            x.Resume == null ? null : x.Resume.Id, x.Resume != null && x.Resume.IsPrimary,
            x.Resume == null ? 0 : applications.Count(a => a.ResumeId == x.Resume.Id)));
    }
    public async Task<Page<DocumentView>> List(int page, string? type, CancellationToken ct)
    {
        if (page < 1 || page > 100000) throw new DomainValidationException("Invalid page.");
        var rows = Rows();
        if (type != null)
        {
            if (!Enum.TryParse<DocumentType>(type, out var parsed) || !Enum.IsDefined(parsed) || parsed.ToString() != type) throw new DomainValidationException("Invalid document type.");
            rows = rows.Where(x => x.Document.Type == parsed);
        }
        var count = await rows.CountAsync(ct);
        return new(await Views(rows.OrderByDescending(x => x.Document.CreatedAtUtc).ThenBy(x => x.Document.Id).Skip((page - 1) * 20).Take(20)).ToListAsync(ct), page, 20, count);
    }
    public async Task<DocumentView> Upload(Stream stream, string mime, string filename, string name, string type, CancellationToken ct)
    {
        if (!Enum.TryParse<DocumentType>(type, out var parsed) || !Enum.IsDefined(parsed) || parsed.ToString() != type) throw new DomainValidationException("Choose a document type.");
        var safe = LocalDocumentStore.SafeName(filename);
        var bytes = await LocalDocumentStore.Validate(stream, mime, ct);
        var key = await store.Write(bytes, ct);
        var committed = false;
        try
        {
            await using var tx = await db.Database.BeginTransactionAsync(ct); await Lock(ct);
            var document = new Document(current.UserId, name, safe, key, "application/pdf", bytes.Length, parsed);
            owned.Add(document);
            if (parsed == DocumentType.Resume)
            {
                var resume = new Resume(document, name);
                resume.SetPrimary(!await owned.Query<Resume>().AnyAsync(ct)); owned.Add(resume);
            }
            await owned.SaveChangesAsync(ct); await tx.CommitAsync(ct); committed = true;
            return await Views(Rows().Where(x => x.Document.Id == document.Id)).SingleAsync(ct);
        }
        catch
        {
            // An uncertain commit can leave a private orphan, never a database row pointing at a deleted file.
            if (!committed && !await db.Documents.AsNoTracking().AnyAsync(x => x.StorageKey == key, CancellationToken.None)) store.Delete(key);
            throw;
        }
    }
    public async Task Primary(Guid resumeId, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct); await Lock(ct);
        var resume = await owned.Query<Resume>().SingleOrDefaultAsync(x => x.Id == resumeId, ct) ?? throw new DocumentMissingException();
        var previous = await owned.Query<Resume>().Where(x => x.IsPrimary && x.Id != resumeId).ToListAsync(ct);
        foreach (var r in previous) r.SetPrimary(false);
        await owned.SaveChangesAsync(ct); // Release filtered unique index before assigning the new primary.
        resume.SetPrimary(true); await owned.SaveChangesAsync(ct); await tx.CommitAsync(ct);
    }
    public async Task<(Stream Stream, string Name)> Download(Guid id, CancellationToken ct)
    {
        var d = await Find(id, ct);
        try { return (store.Open(d.StorageKey), d.OriginalFileName); }
        catch (FileNotFoundException) { throw new DocumentMissingException(); }
        catch (DirectoryNotFoundException) { throw new DocumentMissingException(); }
    }
    public async Task Delete(Guid id, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct); await Lock(ct);
        var d = await Find(id, ct);
        var r = await owned.Query<Resume>().SingleOrDefaultAsync(x => x.DocumentId == id, ct);
        if (r != null)
        {
            if (await owned.Query<JobApplication>().AnyAsync(x => x.ResumeId == r.Id, ct)) throw new DocumentConflictException("This resume is assigned to an application. Remove its assignment before deleting it, including archived applications.");
            foreach (var skill in await owned.Query<ResumeSkill>().Where(x => x.ResumeId == r.Id).ToListAsync(ct)) owned.Remove(skill);
            await owned.SaveChangesAsync(ct); owned.Remove(r); await owned.SaveChangesAsync(ct);
        }
        owned.Remove(d); await owned.SaveChangesAsync(ct); await tx.CommitAsync(ct);
        // Deleting bytes only after commit preserves references on rollback/crash. Failed cleanup leaves an inaccessible orphan.
        try { store.Delete(d.StorageKey); }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { logger.LogWarning("Document byte cleanup failed for {DocumentId}", id); }
    }
}
