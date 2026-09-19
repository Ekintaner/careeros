using CareerOS.Domain.Accounts;
using CareerOS.Domain.Applications;
using CareerOS.Domain.Common;
using CareerOS.Domain.Documents;
using CareerOS.Domain.Opportunities;
using CareerOS.Domain.Planning;
using CareerOS.Domain.Skills;
using Xunit;

namespace CareerOS.Tests.Unit;

public sealed class DomainTests
{
    private static Job Job() => new(new Company(Guid.NewGuid(), "Example"), "Engineer");

    [Fact]
    public void NamesNormalizeWithoutLosingSkillPunctuation()
    {
        Assert.Equal("person@example.com", new User(" Person@Example.COM ", "Person").EmailNormalized);
        Assert.Equal("c#", new Skill(" C# ").NormalizedName);
        Assert.Equal(".net", new Skill(".NET").NormalizedName);
        Assert.Throws<DomainValidationException>(() => new Skill(" "));
        Assert.Throws<DomainValidationException>(() => new User("Not an email", "Person"));
    }

    [Theory]
    [InlineData(-1, 10)]
    [InlineData(20, 10)]
    [InlineData(1.001, 10)]
    public void SalaryRejectsInvalidAmounts(decimal minimum, decimal maximum)
    {
        var job = Job();
        Assert.Throws<DomainValidationException>(() => job.SetSalary(minimum, maximum, "USD", SalaryPeriod.Year));
        Assert.Null(job.SalaryMin);
    }

    [Fact]
    public void SalaryRequiresCurrencyAndPeriodAndUpdatesAtomically()
    {
        var job = Job();
        job.SetSalary(100.25m, 200.50m, "usd", SalaryPeriod.Hour);
        Assert.Equal("USD", job.SalaryCurrency);
        Assert.Throws<DomainValidationException>(() => job.SetSalary(100, 200, null, SalaryPeriod.Year));
        Assert.Equal(100.25m, job.SalaryMin);
        Assert.Throws<DomainValidationException>(() => job.SetSalary(null, null, "USD", null));
        job.SetSalary(null, null, null, null);
        Assert.Null(job.SalaryCurrency);
    }

    [Fact]
    public void InterviewsRequirePositiveUtcTimeRange()
    {
        var application = new JobApplication(Job());
        var start = new DateTime(2026, 9, 20, 12, 0, 0, DateTimeKind.Utc);
        Assert.Throws<DomainValidationException>(() => new Interview(application, InterviewType.Technical, start, start));
        Assert.Throws<DomainValidationException>(() => new Interview(application, InterviewType.Technical, start, start.AddHours(-1)));
        Assert.Throws<DomainValidationException>(() => new Interview(application, InterviewType.Technical, DateTime.SpecifyKind(start, DateTimeKind.Unspecified), start.AddHours(1)));
        var interview = new Interview(application, InterviewType.Technical, start, start.AddHours(1));
        Assert.Throws<DomainValidationException>(() => interview.SetVenue("Office", "javascript:alert(1)"));
        Assert.Throws<DomainValidationException>(() => interview.SetVenue("Office", "https://example.com/room with spaces"));
        Assert.Null(interview.Location);
    }

    [Fact]
    public void FileMetadataAndResumeTypeAreValidated()
    {
        var userId = Guid.NewGuid();
        Assert.Throws<DomainValidationException>(() => new Document(userId, "CV", "cv.pdf", "key", "application/pdf", -1, DocumentType.Resume));
        var letter = new Document(userId, "Letter", "letter.pdf", "key", "application/pdf", 1, DocumentType.CoverLetter);
        Assert.Throws<DomainValidationException>(() => new Resume(letter, "CV"));
    }

    [Fact]
    public void ApplicationTransitionsAppendOrderedHistoryAndProtectTerminalState()
    {
        var clock = new TestClock();
        var application = new JobApplication(Job(), clock);
        Assert.Single(application.StatusHistory);
        Assert.Null(application.StatusHistory.Single().FromStatus);
        Assert.Throws<DomainValidationException>(() => application.TransitionTo(ApplicationStatus.Accepted, clock));
        application.TransitionTo(ApplicationStatus.Applied, clock);
        clock.Advance();
        application.TransitionTo(ApplicationStatus.Interview, clock);
        application.TransitionTo(ApplicationStatus.Offer, clock);
        application.TransitionTo(ApplicationStatus.Accepted, clock);
        application.TransitionTo(ApplicationStatus.Archived, clock);
        Assert.Equal(Enumerable.Range(0, 6), application.StatusHistory.Select(h => h.Sequence));
        Assert.Equal(ApplicationStatus.Archived, application.Status);
        Assert.NotNull(application.AppliedAtUtc);
        Assert.Throws<DomainValidationException>(() => application.TransitionTo(ApplicationStatus.Applied, clock));
    }

    [Fact]
    public void RelatedRecordsCannotCrossOwners()
    {
        var application = new JobApplication(Job());
        var resume = new Resume(new Document(Guid.NewGuid(), "CV", "cv.pdf", "key", "application/pdf", 1, DocumentType.Resume), "CV");
        Assert.Throws<DomainValidationException>(() => application.AttachResume(resume));
        Assert.Throws<DomainValidationException>(() => new CareerTask(Guid.NewGuid(), "Follow up").LinkApplication(application));
    }

    [Fact]
    public void CompletionIsIdempotentAndCanBeReopened()
    {
        var clock = new TestClock();
        var task = new CareerTask(Guid.NewGuid(), "Follow up");
        task.Complete(clock);
        var first = task.CompletedAtUtc;
        clock.Advance();
        task.Complete(clock);
        Assert.Equal(first, task.CompletedAtUtc);
        task.Reopen();
        Assert.Null(task.CompletedAtUtc);
    }

    [Fact]
    public void ProfileRejectsInvalidExperienceWithoutPartialMutation()
    {
        var profile = new CareerProfile(Guid.NewGuid());
        Assert.Throws<DomainValidationException>(() => profile.Update("Headline", null, null, -1, WorkMode.Remote));
        Assert.Null(profile.Headline);
        Assert.Throws<DomainValidationException>(() => profile.Update(null, null, null, 1, (WorkMode)999));
    }

    private sealed class TestClock : TimeProvider
    {
        private DateTimeOffset now = new(2026, 9, 20, 12, 0, 0, TimeSpan.Zero);
        public override DateTimeOffset GetUtcNow() => now;
        public void Advance() => now = now.AddHours(1);
    }
}
