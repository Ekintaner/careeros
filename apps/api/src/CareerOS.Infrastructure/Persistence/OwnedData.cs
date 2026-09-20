using CareerOS.Application.Authentication;
using CareerOS.Domain.Accounts;
using CareerOS.Domain.Applications;
using CareerOS.Domain.Documents;
using CareerOS.Domain.Notes;
using CareerOS.Domain.Notifications;
using CareerOS.Domain.Opportunities;
using CareerOS.Domain.Planning;
using CareerOS.Domain.Skills;
using Microsoft.EntityFrameworkCore;

namespace CareerOS.Infrastructure.Persistence;

// Explicit authorization boundary for future product handlers. Auth workflows use their own narrowly scoped queries.
public sealed class OwnedData(CareerOSDbContext db, ICurrentUser current)
{
    private static readonly HashSet<Type> Types = [typeof(CareerProfile), typeof(Company), typeof(Job), typeof(JobApplication),
        typeof(ApplicationStatusHistory), typeof(Document), typeof(Resume), typeof(Interview), typeof(CareerTask),
        typeof(ApplicationNote), typeof(InterviewNote), typeof(Notification), typeof(UserSkill), typeof(JobSkill), typeof(ResumeSkill)];
    public IQueryable<T> Query<T>() where T : class
    {
        DemandType(typeof(T));
        var owner = current.UserId;
        return db.Set<T>().Where(x => EF.Property<Guid>(x, "UserId") == owner);
    }
    public void Add<T>(T entity) where T : class
    {
        DemandType(typeof(T));
        DemandOwner(entity);
        db.Add(entity);
    }
    public void Remove<T>(T entity) where T : class
    {
        DemandType(typeof(T));
        DemandOwner(entity);
        db.Remove(entity);
    }
    public Task<int> SaveChangesAsync(CancellationToken ct = default)
    {
        _ = current.UserId;
        db.ChangeTracker.DetectChanges();
        foreach (var entry in db.ChangeTracker.Entries().Where(x => x.State is EntityState.Added or EntityState.Modified or EntityState.Deleted))
        {
            DemandType(entry.Entity.GetType());
            DemandOwner(entry.Entity);
            if (entry.State != EntityState.Added && (Guid)entry.Property("UserId").OriginalValue! != current.UserId)
                throw new UnauthorizedAccessException("Resource is not available.");
        }
        return db.SaveChangesAsync(ct);
    }
    private static void DemandType(Type type)
    {
        if (!Types.Contains(type)) throw new InvalidOperationException("This entity requires a separate authorization policy.");
    }
    private void DemandOwner(object entity)
    {
        if ((Guid)db.Entry(entity).Property("UserId").CurrentValue! != current.UserId)
            throw new UnauthorizedAccessException("Resource is not available.");
    }
}
