using CareerOS.Application.Applications;
using CareerOS.Application.Authentication;
using CareerOS.Application.Opportunities;
using CareerOS.Application.Planning;
using CareerOS.Domain.Applications;
using CareerOS.Domain.Common;
using CareerOS.Domain.Planning;
using CareerOS.Domain.Notes;
using CareerOS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
namespace CareerOS.Infrastructure.Planning;

public sealed class PlanningService(CareerOSDbContext db, OwnedData owned, ICurrentUser current, TimeProvider clock)
{
    private uint Version(object entity) => (uint)db.Entry(entity).Property("Version").CurrentValue!;
    private void Expect(object entity, uint version)
    {
        if (version == 0 || Version(entity) != version) throw new ApplicationConflictException("This record changed. Reload before editing again.");
        db.Entry(entity).Property("Version").OriginalValue = version;
    }
    private async Task<T> Find<T>(Guid id, CancellationToken ct) where T : class => await owned.Query<T>().SingleOrDefaultAsync(x => EF.Property<Guid>(x, "Id") == id, ct) ?? throw new ApplicationNotFoundException();
    private InterviewView View(Interview x) => new(x.Id, x.ApplicationId, x.Type.ToString(), x.ScheduledStartUtc, x.ScheduledEndUtc, x.Location, x.MeetingUrl, x.Outcome, x.CreatedAtUtc, x.UpdatedAtUtc, Version(x));
    private TaskView View(CareerTask x) => new(x.Id, x.ApplicationId, x.Title, x.Description, x.DueAtUtc, x.Priority.ToString(), x.CompletedAtUtc, x.CreatedAtUtc, x.UpdatedAtUtc, Version(x));
    public async Task<Page<InterviewView>> Interviews(PlanningQuery q, CancellationToken ct)
    {
        q.Validate(false); var now = clock.GetUtcNow().UtcDateTime;
        var rows = owned.Query<Interview>().AsNoTracking();
        if (q.ApplicationId.HasValue) rows = rows.Where(x => x.ApplicationId == q.ApplicationId);
        if (q.View is null or "upcoming") rows = rows.Where(x => x.ScheduledEndUtc > now);
        if (q.View == "past") rows = rows.Where(x => x.ScheduledEndUtc <= now);
        var count = await rows.CountAsync(ct);
        var sorted = q.View == "past" ? rows.OrderByDescending(x => x.ScheduledStartUtc) : rows.OrderBy(x => x.ScheduledStartUtc);
        var items = await sorted.ThenBy(x => x.Id).Skip((q.Page - 1) * q.PageSize).Take(q.PageSize).Select(x => new InterviewView(x.Id, x.ApplicationId, x.Type.ToString(), x.ScheduledStartUtc, x.ScheduledEndUtc, x.Location, x.MeetingUrl, x.Outcome, x.CreatedAtUtc, x.UpdatedAtUtc, EF.Property<uint>(x, "Version"))).ToListAsync(ct);
        return new(items, q.Page, q.PageSize, count);
    }
    public async Task<InterviewView> Interview(Guid id, CancellationToken ct) => View(await Find<Interview>(id, ct));
    public async Task<InterviewView> SaveInterview(Guid? id, InterviewInput input, CancellationToken ct)
    {
        Interview x;
        var type = OpportunityValidation.EnumValue<InterviewType>(input.Type) ?? throw new DomainValidationException("Choose an interview type.");
        if (id.HasValue)
        {
            x = await Find<Interview>(id.Value, ct); Expect(x, input.Version);
            if (x.ApplicationId != input.ApplicationId) throw new DomainValidationException("An interview's application cannot be changed.");
            x.ChangeType(type); x.Reschedule(input.ScheduledStartUtc, input.ScheduledEndUtc);
        }
        else { x = new Interview(await Find<JobApplication>(input.ApplicationId, ct), type, input.ScheduledStartUtc, input.ScheduledEndUtc); owned.Add(x); }
        x.SetVenue(input.Location, input.MeetingUrl); x.RecordOutcome(input.Outcome);
        await owned.SaveChangesAsync(ct); return View(x);
    }
    public async Task DeleteInterview(Guid id, uint version, CancellationToken ct)
    {
        var x = await Find<Interview>(id, ct); Expect(x, version);
        // Notes are retained until explicitly deleted, including notes added concurrently.
        if (await owned.Query<InterviewNote>().AnyAsync(n => n.InterviewId == id, ct)) throw new ApplicationConflictException("Delete this interview's notes before deleting the interview.");
        owned.Remove(x); await owned.SaveChangesAsync(ct);
    }
    public async Task<Page<TaskView>> Tasks(PlanningQuery q, CancellationToken ct)
    {
        q.Validate(true); var now = clock.GetUtcNow().UtcDateTime;
        var rows = owned.Query<CareerTask>().AsNoTracking();
        if (q.ApplicationId.HasValue) rows = rows.Where(x => x.ApplicationId == q.ApplicationId);
        if (q.View is null or "open" or "overdue" or "upcoming") rows = rows.Where(x => x.CompletedAtUtc == null);
        if (q.View == "completed") rows = rows.Where(x => x.CompletedAtUtc != null);
        if (q.View == "overdue") rows = rows.Where(x => x.DueAtUtc < now);
        if (q.View == "upcoming") rows = rows.Where(x => x.DueAtUtc >= now && x.DueAtUtc <= now.AddDays(7));
        var count = await rows.CountAsync(ct);
        var items = await rows.OrderBy(x => x.DueAtUtc == null).ThenBy(x => x.DueAtUtc).ThenByDescending(x => x.Priority == TaskPriority.High ? 2 : x.Priority == TaskPriority.Normal ? 1 : 0).ThenBy(x => x.Id).Skip((q.Page - 1) * q.PageSize).Take(q.PageSize).Select(x => new TaskView(x.Id, x.ApplicationId, x.Title, x.Description, x.DueAtUtc, x.Priority.ToString(), x.CompletedAtUtc, x.CreatedAtUtc, x.UpdatedAtUtc, EF.Property<uint>(x, "Version"))).ToListAsync(ct);
        return new(items, q.Page, q.PageSize, count);
    }
    public async Task<TaskView> Task(Guid id, CancellationToken ct) => View(await Find<CareerTask>(id, ct));
    public async Task<TaskView> SaveTask(Guid? id, TaskInput input, CancellationToken ct)
    {
        var x = id.HasValue ? await Find<CareerTask>(id.Value, ct) : new CareerTask(current.UserId, input.Title);
        if (id.HasValue) Expect(x, input.Version); else owned.Add(x);
        var application = input.ApplicationId.HasValue ? await Find<JobApplication>(input.ApplicationId.Value, ct) : null;
        x.Rename(input.Title); x.Plan(input.Description, input.DueAtUtc, OpportunityValidation.EnumValue<TaskPriority>(input.Priority) ?? throw new DomainValidationException("Choose a priority."));
        x.LinkApplication(application); if (input.Completed) x.Complete(clock); else x.Reopen();
        await owned.SaveChangesAsync(ct); return View(x);
    }
    public async Task DeleteTask(Guid id, uint version, CancellationToken ct)
    { var x = await Find<CareerTask>(id, ct); Expect(x, version); owned.Remove(x); await owned.SaveChangesAsync(ct); }
    public async Task<Page<NoteView>> Notes(Guid id, int page, CancellationToken ct)
    {
        new ApplicationQuery(Page: page).Validate(); await Find<Interview>(id, ct);
        var rows = owned.Query<InterviewNote>().AsNoTracking().Where(x => x.InterviewId == id); var count = await rows.CountAsync(ct);
        var items = await rows.OrderByDescending(x => x.CreatedAtUtc).ThenBy(x => x.Id).Skip((page - 1) * 20).Take(20)
            .Select(x => new NoteView(x.Id, x.Body, x.CreatedAtUtc, x.UpdatedAtUtc, EF.Property<uint>(x, "Version"))).ToListAsync(ct);
        return new(items, page, 20, count);
    }
    public async Task<NoteView> SaveNote(Guid id, Guid? noteId, NoteInput input, CancellationToken ct)
    {
        var a = await Find<Interview>(id, ct); InterviewNote note;
        if (noteId.HasValue)
        {
            note = await owned.Query<InterviewNote>().SingleOrDefaultAsync(x => x.InterviewId == id && x.Id == noteId, ct) ?? throw new ApplicationNotFoundException();
            Expect(note, input.Version); note.Edit(input.Body);
        }
        else { note = new InterviewNote(a, input.Body); owned.Add(note); }
        await owned.SaveChangesAsync(ct); return new(note.Id, note.Body, note.CreatedAtUtc, note.UpdatedAtUtc, Version(note));
    }
    public async Task DeleteNote(Guid id, Guid noteId, uint version, CancellationToken ct)
    {
        await Find<Interview>(id, ct);
        var note = await owned.Query<InterviewNote>().SingleOrDefaultAsync(x => x.InterviewId == id && x.Id == noteId, ct) ?? throw new ApplicationNotFoundException();
        Expect(note, version); owned.Remove(note); await owned.SaveChangesAsync(ct);
    }
}
