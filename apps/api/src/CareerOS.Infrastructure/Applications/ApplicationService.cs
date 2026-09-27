using CareerOS.Application.Applications;
using CareerOS.Application.Opportunities;
using CareerOS.Domain.Applications;
using CareerOS.Domain.Common;
using CareerOS.Domain.Documents;
using CareerOS.Domain.Notes;
using CareerOS.Domain.Opportunities;
using CareerOS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CareerOS.Infrastructure.Applications;

public sealed class ApplicationService(CareerOSDbContext db, OwnedData owned, TimeProvider clock)
{
    private uint Version(object entity) => (uint)db.Entry(entity).Property("Version").CurrentValue!;
    private void Expect(object entity, uint version)
    {
        if (version == 0 || Version(entity) != version) throw new ApplicationConflictException("This record changed. Reload the latest version before editing again.");
        db.Entry(entity).Property("Version").OriginalValue = version;
    }
    private async Task<JobApplication> Find(Guid id, CancellationToken ct) => await owned.Query<JobApplication>().SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw new ApplicationNotFoundException();
    private async Task<Resume?> Resume(Guid? id, CancellationToken ct) => id.HasValue ? await owned.Query<Resume>().SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw new ApplicationNotFoundException() : null;
    private static string? Pattern(string? text) => OpportunityValidation.Text(text) is { } value ? "%" + value.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_") + "%" : null;
    private sealed class Row
    {
        public required JobApplication Application { get; init; }
        public required Job Job { get; init; }
        public required Company Company { get; init; }
    }
    private IQueryable<Row> Rows(ApplicationQuery q)
    {
        q.Validate();
        var rows = from a in owned.Query<JobApplication>().AsNoTracking()
                   join j in owned.Query<Job>().AsNoTracking() on a.JobId equals j.Id
                   join c in owned.Query<Company>().AsNoTracking() on j.CompanyId equals c.Id
                   select new Row { Application = a, Job = j, Company = c };
        var status = OpportunityValidation.EnumValue<ApplicationStatus>(q.Status); var pattern = Pattern(q.Search);
        if (q.State is null or "active") rows = rows.Where(x => x.Application.Status != ApplicationStatus.Archived);
        if (q.State == "archived") rows = rows.Where(x => x.Application.Status == ApplicationStatus.Archived);
        if (status.HasValue) rows = rows.Where(x => x.Application.Status == status);
        if (q.CompanyId.HasValue) rows = rows.Where(x => x.Company.Id == q.CompanyId);
        if (q.JobId.HasValue) rows = rows.Where(x => x.Job.Id == q.JobId);
        if (q.AppliedFrom.HasValue) rows = rows.Where(x => x.Application.AppliedAtUtc >= q.AppliedFrom);
        if (q.AppliedTo.HasValue) rows = rows.Where(x => x.Application.AppliedAtUtc <= q.AppliedTo);
        if (pattern is not null) rows = rows.Where(x => EF.Functions.ILike(x.Job.Title, pattern, "\\") || EF.Functions.ILike(x.Company.Name, pattern, "\\"));
        return rows;
    }
    public async Task<Page<ApplicationCard>> List(ApplicationQuery q, CancellationToken ct)
    {
        var rows = Rows(q); var count = await rows.CountAsync(ct);
        var sorted = q.Sort switch
        {
            "oldest" => rows.OrderBy(x => x.Application.CreatedAtUtc),
            "updated" => rows.OrderByDescending(x => x.Application.UpdatedAtUtc),
            "applied" => rows.OrderBy(x => x.Application.AppliedAtUtc == null).ThenByDescending(x => x.Application.AppliedAtUtc),
            "company" => rows.OrderBy(x => x.Company.Name),
            "title" => rows.OrderBy(x => x.Job.Title),
            _ => rows.OrderByDescending(x => x.Application.CreatedAtUtc)
        };
        var items = await sorted.ThenBy(x => x.Application.Id).Skip((q.Page - 1) * q.PageSize).Take(q.PageSize)
            .Select(x => new ApplicationCard(x.Application.Id, x.Job.Id, x.Company.Id, x.Job.Title, x.Company.Name, x.Job.Location,
                x.Application.Status.ToString(), x.Application.ResumeId, x.Application.AppliedAtUtc, x.Application.CreatedAtUtc,
                x.Application.UpdatedAtUtc, EF.Property<uint>(x.Application, "Version"))).ToListAsync(ct);
        return new(items, q.Page, q.PageSize, count);
    }
    public async Task<IReadOnlyList<StageCount>> Summary(ApplicationQuery q, CancellationToken ct)
    {
        var counts = await Rows(q).GroupBy(x => x.Application.Status).Select(g => new { Status = g.Key, Count = g.Count() }).ToListAsync(ct);
        return Enum.GetValues<ApplicationStatus>().Select(s => new StageCount(s.ToString(), counts.SingleOrDefault(x => x.Status == s)?.Count ?? 0)).ToList();
    }
    public async Task<ApplicationView> Get(Guid id, CancellationToken ct)
    {
        var result = await (from a in owned.Query<JobApplication>().AsNoTracking()
                            join j in owned.Query<Job>() on a.JobId equals j.Id
                            join c in owned.Query<Company>() on j.CompanyId equals c.Id
                            join r in owned.Query<Resume>() on a.ResumeId equals r.Id into resumes
                            from r in resumes.DefaultIfEmpty()
                            where a.Id == id
                            select new ApplicationView(a.Id, j.Id, c.Id, j.Title, c.Name, j.Location, a.Status.ToString(), a.ResumeId,
                                r == null ? null : r.Name, a.Source, j.SourceUrl, a.AppliedAtUtc, a.CreatedAtUtc, a.UpdatedAtUtc, a.LastStatusChangedAtUtc, EF.Property<uint>(a, "Version"))).SingleOrDefaultAsync(ct);
        return result ?? throw new ApplicationNotFoundException();
    }
    public async Task<ApplicationView> Create(CreateApplicationInput input, CancellationToken ct)
    {
        var job = await owned.Query<Job>().SingleOrDefaultAsync(x => x.Id == input.JobId, ct) ?? throw new ApplicationNotFoundException();
        var existing = await owned.Query<JobApplication>().Where(x => x.JobId == job.Id).Select(x => (Guid?)x.Id).SingleOrDefaultAsync(ct);
        if (existing.HasValue) throw new ApplicationConflictException("This job already has an application.", existing);
        var status = OpportunityValidation.EnumValue<ApplicationStatus>(input.InitialStatus) ?? throw new DomainValidationException("Choose Saved or Applied.");
        var resume = await Resume(input.ResumeId, ct);
        var a = new JobApplication(job, clock, status); a.SetSource(input.Source); a.AttachResume(resume);
        if (input.AppliedAtUtc.HasValue) a.CorrectAppliedDate(input.AppliedAtUtc);
        owned.Add(a); await owned.SaveChangesAsync(ct); return await Get(a.Id, ct);
    }
    public async Task<ApplicationView> Edit(Guid id, EditApplicationInput input, CancellationToken ct)
    {
        var a = await Find(id, ct); Expect(a, input.Version); var resume = await Resume(input.ResumeId, ct);
        a.SetSource(input.Source); a.CorrectAppliedDate(input.AppliedAtUtc); a.AttachResume(resume);
        await owned.SaveChangesAsync(ct); return await Get(id, ct);
    }
    public async Task<ApplicationView> Transition(Guid id, StatusInput input, CancellationToken ct)
    {
        var a = await Find(id, ct); Expect(a, input.Version);
        a.TransitionTo(OpportunityValidation.EnumValue<ApplicationStatus>(input.Status) ?? throw new DomainValidationException("Choose a status."), clock);
        await owned.SaveChangesAsync(ct); return await Get(id, ct);
    }
    public async Task<ApplicationView> Archive(Guid id, VersionInput input, bool restore, CancellationToken ct)
    {
        var a = await Find(id, ct); Expect(a, input.Version);
        if (restore)
        {
            var last = await owned.Query<ApplicationStatusHistory>().SingleAsync(x => x.ApplicationId == id && x.Sequence == a.StatusRevision, ct);
            a.Restore(last, clock);
        }
        else a.TransitionTo(ApplicationStatus.Archived, clock);
        await owned.SaveChangesAsync(ct); return await Get(id, ct);
    }
    public async Task Delete(Guid id, CancellationToken ct)
    { await Find(id, ct); throw new ApplicationConflictException("Applications retain their history. Archive this application instead of deleting it."); }
    public async Task<Page<HistoryView>> History(Guid id, int page, CancellationToken ct)
    {
        new ApplicationQuery(Page: page).Validate(); await Find(id, ct);
        var rows = owned.Query<ApplicationStatusHistory>().AsNoTracking().Where(x => x.ApplicationId == id);
        var count = await rows.CountAsync(ct);
        var items = await rows.OrderBy(x => x.Sequence).Skip((page - 1) * 50).Take(50)
            .Select(x => new HistoryView(x.Id, x.FromStatus == null ? null : x.FromStatus.ToString(), x.ToStatus.ToString(), x.Sequence, x.ChangedAtUtc)).ToListAsync(ct);
        return new(items, page, 50, count);
    }
    public async Task<Page<NoteView>> Notes(Guid id, int page, CancellationToken ct)
    {
        new ApplicationQuery(Page: page).Validate(); await Find(id, ct);
        var rows = owned.Query<ApplicationNote>().AsNoTracking().Where(x => x.ApplicationId == id); var count = await rows.CountAsync(ct);
        var items = await rows.OrderByDescending(x => x.CreatedAtUtc).ThenBy(x => x.Id).Skip((page - 1) * 20).Take(20)
            .Select(x => new NoteView(x.Id, x.Body, x.CreatedAtUtc, x.UpdatedAtUtc, EF.Property<uint>(x, "Version"))).ToListAsync(ct);
        return new(items, page, 20, count);
    }
    public async Task<NoteView> SaveNote(Guid id, Guid? noteId, NoteInput input, CancellationToken ct)
    {
        var a = await Find(id, ct); ApplicationNote note;
        if (noteId.HasValue)
        {
            note = await owned.Query<ApplicationNote>().SingleOrDefaultAsync(x => x.ApplicationId == id && x.Id == noteId, ct) ?? throw new ApplicationNotFoundException();
            Expect(note, input.Version); note.Edit(input.Body);
        }
        else { note = new ApplicationNote(a, input.Body); owned.Add(note); }
        await owned.SaveChangesAsync(ct); return new(note.Id, note.Body, note.CreatedAtUtc, note.UpdatedAtUtc, Version(note));
    }
    public async Task DeleteNote(Guid id, Guid noteId, uint version, CancellationToken ct)
    {
        await Find(id, ct);
        var note = await owned.Query<ApplicationNote>().SingleOrDefaultAsync(x => x.ApplicationId == id && x.Id == noteId, ct) ?? throw new ApplicationNotFoundException();
        Expect(note, version); owned.Remove(note); await owned.SaveChangesAsync(ct);
    }
    public async Task<Page<ResumeChoice>> Resumes(string? search, CancellationToken ct)
    {
        var rows = owned.Query<Resume>().AsNoTracking(); var pattern = Pattern(search);
        if (pattern is not null) rows = rows.Where(x => EF.Functions.ILike(x.Name, pattern, "\\"));
        var count = await rows.CountAsync(ct);
        return new(await rows.OrderBy(x => x.Name).ThenBy(x => x.Id).Take(20).Select(x => new ResumeChoice(x.Id, x.Name)).ToListAsync(ct), 1, 20, count);
    }
}
