using CareerOS.Domain.Accounts;
using CareerOS.Domain.Applications;
using CareerOS.Domain.Common;
using CareerOS.Domain.Documents;
using CareerOS.Domain.Notes;
using CareerOS.Domain.Notifications;
using CareerOS.Domain.Opportunities;
using CareerOS.Domain.Planning;
using CareerOS.Domain.Skills;
using Microsoft.EntityFrameworkCore;

namespace CareerOS.Infrastructure.Persistence;

public sealed class CareerOSDbContext(DbContextOptions<CareerOSDbContext> options, TimeProvider? clock = null) : DbContext(options)
{
    private readonly TimeProvider clock = clock ?? TimeProvider.System;
    public DbSet<User> Users => Set<User>();
    public DbSet<CareerProfile> CareerProfiles => Set<CareerProfile>();
    public DbSet<Company> Companies => Set<Company>();
    public DbSet<Job> Jobs => Set<Job>();
    public DbSet<JobApplication> Applications => Set<JobApplication>();
    public DbSet<ApplicationStatusHistory> ApplicationStatusHistory => Set<ApplicationStatusHistory>();
    public DbSet<Document> Documents => Set<Document>();
    public DbSet<Resume> Resumes => Set<Resume>();
    public DbSet<Skill> Skills => Set<Skill>();
    public DbSet<UserSkill> UserSkills => Set<UserSkill>();
    public DbSet<JobSkill> JobSkills => Set<JobSkill>();
    public DbSet<ResumeSkill> ResumeSkills => Set<ResumeSkill>();
    public DbSet<Interview> Interviews => Set<Interview>();
    public DbSet<CareerTask> CareerTasks => Set<CareerTask>();
    public DbSet<ApplicationNote> ApplicationNotes => Set<ApplicationNote>();
    public DbSet<InterviewNote> InterviewNotes => Set<InterviewNote>();
    public DbSet<Notification> Notifications => Set<Notification>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(CareerOSDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        PrepareChanges();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        PrepareChanges();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    private void PrepareChanges()
    {
        ChangeTracker.DetectChanges();
        var now = clock.GetUtcNow().UtcDateTime;
        foreach (var entry in ChangeTracker.Entries())
        {
            if (entry.Entity is ApplicationStatusHistory && entry.State is EntityState.Modified or EntityState.Deleted)
                throw new InvalidOperationException("Status history is append-only.");

            if (entry.State is not (EntityState.Added or EntityState.Modified)) continue;
            if (entry.State == EntityState.Modified && entry.Metadata.FindProperty("UserId") is not null
                && entry.Property("UserId").IsModified)
                throw new InvalidOperationException("Ownership cannot be changed.");

            if (entry.Entity is ICreatedEntity)
            {
                var created = entry.Property(nameof(ICreatedEntity.CreatedAtUtc));
                if (entry.State == EntityState.Added) created.CurrentValue = now;
                else
                {
                    created.CurrentValue = created.OriginalValue;
                    created.IsModified = false;
                }
            }

            if (entry.Entity is IAuditedEntity)
                entry.Property(nameof(IAuditedEntity.UpdatedAtUtc)).CurrentValue = now;

            // These events use authoritative save time, including completion before first persistence.
            if (entry.Entity is CareerTask { CompletedAtUtc: not null }
                && (entry.State == EntityState.Added || entry.Property(nameof(CareerTask.CompletedAtUtc)).IsModified))
                entry.Property(nameof(CareerTask.CompletedAtUtc)).CurrentValue = now;
            if (entry.Entity is Notification { ReadAtUtc: not null }
                && (entry.State == EntityState.Added || entry.Property(nameof(Notification.ReadAtUtc)).IsModified))
                entry.Property(nameof(Notification.ReadAtUtc)).CurrentValue = now;

            foreach (var property in entry.Properties)
                if (property.CurrentValue is DateTime timestamp && timestamp.Kind != DateTimeKind.Utc)
                    throw new InvalidOperationException($"UTC is required for {entry.Metadata.ClrType.Name}.{property.Metadata.Name}.");
        }
    }
}
