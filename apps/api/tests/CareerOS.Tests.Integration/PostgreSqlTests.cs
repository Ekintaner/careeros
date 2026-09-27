using CareerOS.Domain.Accounts;
using CareerOS.Domain.Applications;
using CareerOS.Domain.Documents;
using CareerOS.Domain.Notes;
using CareerOS.Domain.Notifications;
using CareerOS.Domain.Opportunities;
using CareerOS.Domain.Planning;
using CareerOS.Domain.Skills;
using CareerOS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;
using Xunit;

namespace CareerOS.Tests.Integration;

[Collection("PostgreSQL")]
public sealed class PostgreSqlTests(PostgreSqlFixture fixture)
{
    [PostgreSqlFact]
    public async Task MigrationCreatesSchemaRollsBackAndReapplies()
    {
        await using var database = await fixture.CreateDatabaseAsync();
        await using var context = database.Context();
        await context.Database.MigrateAsync();
        Assert.False(context.Database.HasPendingModelChanges());
        Assert.Equal(19L, await database.ScalarAsync<long>("SELECT count(*) FROM information_schema.tables WHERE table_schema = 'public' AND table_name <> '__EFMigrationsHistory'"));
        Assert.Equal(0L, await database.ScalarAsync<long>("SELECT count(*) FROM pg_constraint WHERE contype = 'f' AND confdeltype <> 'r'"));
        Assert.True(await database.ScalarAsync<bool>("SELECT EXISTS (SELECT 1 FROM pg_indexes WHERE tablename = 'resumes' AND indexdef LIKE '%UNIQUE%' AND indexdef LIKE '%WHERE%')"));
        Assert.True(await database.ScalarAsync<bool>("SELECT EXISTS (SELECT 1 FROM pg_indexes WHERE tablename = 'job_applications' AND indexdef LIKE '%UNIQUE%' AND indexdef LIKE '%\"UserId\", \"JobId\"%')"));
        Assert.True(await database.ScalarAsync<bool>("SELECT EXISTS (SELECT 1 FROM pg_indexes WHERE tablename = 'application_status_history' AND indexdef LIKE '%\"ApplicationId\", \"ChangedAtUtc\", \"Sequence\"%')"));
        Assert.Equal(0L, await database.ScalarAsync<long>("SELECT count(*) FROM information_schema.columns WHERE table_schema = 'public' AND column_name LIKE '%Utc' AND data_type <> 'timestamp with time zone'"));
        Assert.Equal(5, (await context.Database.GetAppliedMigrationsAsync()).Count());
        await context.GetService<IMigrator>().MigrateAsync("0");
        Assert.Equal(0L, await database.ScalarAsync<long>("SELECT count(*) FROM information_schema.tables WHERE table_schema = 'public' AND table_name <> '__EFMigrationsHistory'"));
        await context.Database.MigrateAsync();
        Assert.Equal(19L, await database.ScalarAsync<long>("SELECT count(*) FROM information_schema.tables WHERE table_schema = 'public' AND table_name <> '__EFMigrationsHistory'"));
    }

    [PostgreSqlFact]
    public async Task EntireGraphPersistsAndRoundTrips()
    {
        await using var database = await fixture.CreateDatabaseAsync();
        await using var context = database.Context();
        await context.Database.MigrateAsync();
        var graph = await AddGraph(context);
        context.ChangeTracker.Clear();
        Assert.Equal(graph.User.Id, (await context.Jobs.SingleAsync()).UserId);
        Assert.Equal(DocumentType.Resume, (await context.Documents.SingleAsync()).Type);
        Assert.Single(await context.Applications.Include(a => a.StatusHistory).SelectMany(a => a.StatusHistory).ToListAsync());
        Assert.Single(await context.CareerProfiles.ToListAsync());
        Assert.Single(await context.UserSkills.ToListAsync());
        Assert.Single(await context.JobSkills.ToListAsync());
        Assert.Single(await context.ResumeSkills.ToListAsync());
        Assert.Single(await context.ApplicationNotes.ToListAsync());
        Assert.Single(await context.InterviewNotes.ToListAsync());
        Assert.Single(await context.Notifications.ToListAsync());
    }

    [PostgreSqlFact]
    public async Task EmailAndSkillUniquenessUsesDatabaseNormalization()
    {
        await using var database = await fixture.CreateDatabaseAsync();
        await using var context = database.Context();
        await context.Database.MigrateAsync();
        context.AddRange(new User("PERSON@example.com", "Person"), new Skill("Docker"));
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();
        context.Add(new User(" person@EXAMPLE.COM ", "Another"));
        await AssertUnique(context);
        context.ChangeTracker.Clear();
        context.Add(new Skill(" docker "));
        await AssertUnique(context);
        context.ChangeTracker.Clear();
        Assert.Equal("docker", (await context.Skills.SingleAsync()).NormalizedName);
        // Raw writes cannot choose a different normalized value to bypass the unique index.
        var duplicate = await Assert.ThrowsAsync<PostgresException>(() => database.ExecuteAsync(
            $"INSERT INTO skills (\"Id\", \"Name\") VALUES ('{Guid.NewGuid()}', 'DOCKER')"));
        Assert.Equal(PostgresErrorCodes.UniqueViolation, duplicate.SqlState);
    }

    [PostgreSqlFact]
    public async Task OneProfileAndOneApplicationPerUserJobIncludingArchived()
    {
        await using var database = await fixture.CreateDatabaseAsync();
        await using var context = database.Context();
        await context.Database.MigrateAsync();
        var graph = await AddGraph(context);
        graph.Application.TransitionTo(ApplicationStatus.Archived);
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();
        context.Add(new JobApplication(graph.Job));
        await AssertUnique(context);
        context.ChangeTracker.Clear();
        context.Add(new CareerProfile(graph.User.Id));
        await AssertUnique(context);
    }

    [PostgreSqlFact]
    public async Task OnePrimaryResumeAndExplicitTransactionalSwitch()
    {
        await using var database = await fixture.CreateDatabaseAsync();
        await using var context = database.Context();
        await context.Database.MigrateAsync();
        var graph = await AddGraph(context);
        graph.Resume.SetPrimary(true);
        var document = NewDocument(graph.User.Id);
        context.Add(document);
        await context.SaveChangesAsync();
        var second = new Resume(document, "Second");
        second.SetPrimary(true);
        context.Add(second);
        await AssertUnique(context);
        context.Entry(second).State = EntityState.Detached;
        await using var transaction = await context.Database.BeginTransactionAsync();
        graph.Resume.SetPrimary(false);
        await context.SaveChangesAsync();
        context.Add(second);
        await context.SaveChangesAsync();
        await transaction.CommitAsync();
        Assert.Equal(second.Id, (await context.Resumes.SingleAsync(r => r.IsPrimary)).Id);
    }

    [PostgreSqlTheory]
    [InlineData("jobs", "CompanyId")]
    [InlineData("job_applications", "JobId")]
    [InlineData("job_applications", "ResumeId")]
    [InlineData("resumes", "DocumentId")]
    [InlineData("interviews", "ApplicationId")]
    [InlineData("career_tasks", "ApplicationId")]
    [InlineData("application_notes", "ApplicationId")]
    [InlineData("interview_notes", "InterviewId")]
    [InlineData("job_skills", "JobId")]
    [InlineData("resume_skills", "ResumeId")]
    [InlineData("application_status_history", "ApplicationId")]
    public async Task DatabaseRejectsCrossOwnerRelationships(string table, string column)
    {
        await using var database = await fixture.CreateDatabaseAsync();
        await using var context = database.Context();
        await context.Database.MigrateAsync();
        var first = await AddGraph(context);
        var second = await AddGraph(context);
        var unreferencedDocument = NewDocument(second.User.Id);
        context.Add(unreferencedDocument);
        await context.SaveChangesAsync();
        var foreignId = column switch
        {
            "CompanyId" => second.Company.Id,
            "JobId" => second.Job.Id,
            "ResumeId" => second.Resume.Id,
            "DocumentId" => unreferencedDocument.Id,
            "InterviewId" => second.Interview.Id,
            _ => second.Application.Id
        };
        // Avoid an existing target event's unique sequence masking the ownership FK.
        var historyFields = table == "application_status_history"
            ? ", \"Sequence\" = 99, \"FromStatus\" = 'Applied', \"ToStatus\" = 'Interview'"
            : "";
        var error = await Assert.ThrowsAsync<PostgresException>(() =>
            database.ExecuteAsync($"UPDATE \"{table}\" SET \"{column}\" = '{foreignId}'{historyFields} WHERE \"UserId\" = '{first.User.Id}'"));
        Assert.Equal(PostgresErrorCodes.ForeignKeyViolation, error.SqlState);
    }

    [PostgreSqlTheory]
    [InlineData("users")]
    [InlineData("companies")]
    [InlineData("jobs")]
    [InlineData("job_applications")]
    [InlineData("documents")]
    [InlineData("resumes")]
    [InlineData("interviews")]
    [InlineData("skills")]
    public async Task ReferencedRecordsCannotBeDeletedSilently(string table)
    {
        await using var database = await fixture.CreateDatabaseAsync();
        await using var context = database.Context();
        await context.Database.MigrateAsync();
        await AddGraph(context);
        var error = await Assert.ThrowsAsync<PostgresException>(() => database.ExecuteAsync($"DELETE FROM \"{table}\""));
        Assert.Equal(PostgresErrorCodes.ForeignKeyViolation, error.SqlState);
        Assert.Equal(1, await context.Applications.CountAsync());
    }

    [PostgreSqlTheory]
    [InlineData("jobs", "\"SalaryMin\" = 200, \"SalaryMax\" = 100, \"SalaryCurrency\" = 'USD', \"SalaryPeriod\" = 'Year'")]
    [InlineData("jobs", "\"SalaryMin\" = 200")]
    [InlineData("jobs", "\"WorkMode\" = 'Imaginary'")]
    [InlineData("documents", "\"FileSizeBytes\" = -1")]
    [InlineData("interviews", "\"ScheduledEndUtc\" = \"ScheduledStartUtc\"")]
    [InlineData("career_tasks", "\"CompletedAtUtc\" = \"CreatedAtUtc\" - interval '1 day'")]
    [InlineData("career_profiles", "\"YearsOfExperience\" = -1")]
    [InlineData("resumes", "\"DocumentType\" = 'CoverLetter'")]
    [InlineData("companies", "\"Website\" = 'javascript:alert(1)'")]
    public async Task DatabaseChecksRejectInvalidWrites(string table, string assignment)
    {
        await using var database = await fixture.CreateDatabaseAsync();
        await using var context = database.Context();
        await context.Database.MigrateAsync();
        await AddGraph(context);
        var error = await Assert.ThrowsAsync<PostgresException>(() => database.ExecuteAsync($"UPDATE \"{table}\" SET {assignment}"));
        Assert.Equal(PostgresErrorCodes.CheckViolation, error.SqlState);
    }

    [PostgreSqlFact]
    public async Task ResumeCannotReferenceWrongDocumentTypeEvenWithSameOwner()
    {
        await using var database = await fixture.CreateDatabaseAsync();
        await using var context = database.Context();
        await context.Database.MigrateAsync();
        var graph = await AddGraph(context);
        var letter = new Document(graph.User.Id, "Letter", "letter.pdf", Guid.NewGuid().ToString(), "application/pdf", 10, DocumentType.CoverLetter);
        context.Add(letter);
        await context.SaveChangesAsync();
        var error = await Assert.ThrowsAsync<PostgresException>(() => database.ExecuteAsync(
            $"UPDATE resumes SET \"DocumentId\" = '{letter.Id}' WHERE \"Id\" = '{graph.Resume.Id}'"));
        Assert.Equal(PostgresErrorCodes.ForeignKeyViolation, error.SqlState);
    }

    [PostgreSqlFact]
    public async Task AuditTimesAreAuthoritativeForSyncAndAsyncSavesAndUtcRoundTrips()
    {
        await using var database = await fixture.CreateDatabaseAsync();
        var clock = new FixedClock(new DateTimeOffset(2026, 9, 20, 12, 0, 0, TimeSpan.Zero));
        await using var context = database.Context(clock);
        await context.Database.MigrateAsync();
        var user = new User("audit@example.com", "Original");
        context.Add(user);
        context.Entry(user).Property(u => u.CreatedAtUtc).CurrentValue = DateTime.UnixEpoch;
        context.SaveChanges();
        Assert.Equal(clock.GetUtcNow().UtcDateTime, user.CreatedAtUtc);
        var createdAt = user.CreatedAtUtc;
        clock.Advance();
        user.Rename("Updated");
        context.Entry(user).Property(u => u.CreatedAtUtc).CurrentValue = DateTime.UnixEpoch;
        await context.SaveChangesAsync();
        Assert.Equal(createdAt, user.CreatedAtUtc);
        Assert.Equal(clock.GetUtcNow().UtcDateTime, user.UpdatedAtUtc);
        context.ChangeTracker.Clear();
        var loaded = await context.Users.SingleAsync();
        Assert.Equal(DateTimeKind.Utc, loaded.CreatedAtUtc.Kind);
        Assert.Equal(createdAt, loaded.CreatedAtUtc);
        var task = new CareerTask(user.Id, "Already complete");
        task.Complete(clock);
        clock.Advance();
        context.Add(task);
        await context.SaveChangesAsync();
        Assert.Equal(task.CreatedAtUtc, task.CompletedAtUtc);
    }

    [PostgreSqlFact]
    public async Task XminPreventsLostUpdates()
    {
        await using var database = await fixture.CreateDatabaseAsync();
        await using var first = database.Context();
        await first.Database.MigrateAsync();
        var graph = await AddGraph(first);
        await using var second = database.Context();
        var stale = await second.Companies.SingleAsync();
        graph.Company.Rename("First writer");
        await first.SaveChangesAsync();
        stale.Rename("Second writer");
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => second.SaveChangesAsync());
    }

    [PostgreSqlFact]
    public async Task StatusAndHistoryPersistAtomicallyAndHistoryIsAppendOnly()
    {
        await using var database = await fixture.CreateDatabaseAsync();
        await using var context = database.Context();
        await context.Database.MigrateAsync();
        var graph = await AddGraph(context);
        context.ChangeTracker.Clear();
        var application = await context.Applications.Include(a => a.StatusHistory).SingleAsync();
        application.TransitionTo(ApplicationStatus.Applied);
        application.TransitionTo(ApplicationStatus.Interview);
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();
        var loaded = await context.Applications.Include(a => a.StatusHistory).SingleAsync();
        Assert.Equal(ApplicationStatus.Interview, loaded.Status);
        Assert.Equal(3, loaded.StatusHistory.Count);
        Assert.Equal(2, loaded.StatusRevision);
        Assert.Equal(ApplicationStatus.Applied, loaded.StatusHistory.Single(h => h.Sequence == 2).FromStatus);
        context.Remove(loaded.StatusHistory.First());
        await Assert.ThrowsAsync<InvalidOperationException>(() => context.SaveChangesAsync());
    }

    [PostgreSqlFact]
    public async Task FailedConcurrentTransitionDoesNotAppendOrphanHistory()
    {
        await using var database = await fixture.CreateDatabaseAsync();
        await using var first = database.Context();
        await first.Database.MigrateAsync();
        await AddGraph(first);
        await using var second = database.Context();
        var winner = await first.Applications.SingleAsync();
        var stale = await second.Applications.SingleAsync();
        winner.TransitionTo(ApplicationStatus.Applied);
        await first.SaveChangesAsync();
        stale.TransitionTo(ApplicationStatus.Withdrawn);
        // Depending on statement ordering, xmin or the unique history sequence detects the race.
        await Assert.ThrowsAnyAsync<DbUpdateException>(() => second.SaveChangesAsync());
        await using var read = database.Context();
        Assert.Equal(ApplicationStatus.Applied, (await read.Applications.SingleAsync()).Status);
        Assert.Equal(2, await read.ApplicationStatusHistory.CountAsync());
    }

    private static async Task AssertUnique(CareerOSDbContext context)
    {
        var error = await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
        Assert.Equal(PostgresErrorCodes.UniqueViolation, Assert.IsType<PostgresException>(error.InnerException).SqlState);
    }

    private static Document NewDocument(Guid userId) =>
        new(userId, "CV", "cv.pdf", Guid.NewGuid().ToString("N"), "application/pdf", 123, DocumentType.Resume);

    internal static async Task<Graph> AddGraph(CareerOSDbContext context)
    {
        var user = new User($"{Guid.NewGuid():N}@example.com", "Test User");
        var company = new Company(user.Id, "Example");
        var job = new Job(company, "Engineer");
        var document = NewDocument(user.Id);
        var resume = new Resume(document, "CV");
        var application = new JobApplication(job);
        application.AttachResume(resume);
        var interview = new Interview(application, InterviewType.Technical, DateTime.UtcNow.AddDays(1), DateTime.UtcNow.AddDays(1).AddHours(1));
        var skill = new Skill("skill-" + Guid.NewGuid().ToString("N"));
        var task = new CareerTask(user.Id, "Follow up");
        task.LinkApplication(application);
        context.AddRange(user, new CareerProfile(user.Id), company, job, document, resume, application,
            interview, skill, task, new UserSkill(user.Id, skill.Id), new JobSkill(job, skill.Id, RequirementLevel.Required),
            new ResumeSkill(resume, skill.Id), new ApplicationNote(application, "Application note"),
            new InterviewNote(interview, "Interview note"), new Notification(user.Id, "reminder", "Reminder", "Prepare"));
        await context.SaveChangesAsync();
        return new Graph(user, company, job, document, resume, application, interview);
    }

    internal sealed record Graph(User User, Company Company, Job Job, Document Document, Resume Resume, JobApplication Application, Interview Interview);
    private sealed class FixedClock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
        public void Advance() => now = now.AddMinutes(1);
    }
}
