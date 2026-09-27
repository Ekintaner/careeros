using CareerOS.Application.Applications;
using CareerOS.Domain.Applications;
using CareerOS.Domain.Common;
using CareerOS.Domain.Notes;
using CareerOS.Domain.Opportunities;
using Xunit;

namespace CareerOS.Tests.Unit;
public sealed class ApplicationTests
{
    private sealed class Clock : TimeProvider { public DateTimeOffset Now = new(2026, 9, 20, 12, 0, 0, TimeSpan.Zero); public override DateTimeOffset GetUtcNow() => Now; }
    private static Job Job() => new(new Company(Guid.NewGuid(), "Company"), "Engineer");
    [Theory]
    [InlineData(ApplicationStatus.Saved)]
    [InlineData(ApplicationStatus.Applied)]
    public void InitialHistoryIsGenuine(ApplicationStatus status)
    {
        var a = new JobApplication(Job(), initialStatus: status); var h = Assert.Single(a.StatusHistory);
        Assert.Null(h.FromStatus); Assert.Equal(status, h.ToStatus); Assert.Equal(0, h.Sequence);
        Assert.Equal(status == ApplicationStatus.Applied, a.AppliedAtUtc.HasValue);
    }
    [Theory]
    [InlineData(ApplicationStatus.Interview)]
    [InlineData(ApplicationStatus.Archived)]
    [InlineData((ApplicationStatus)999)]
    public void InvalidInitialStagesFail(ApplicationStatus status) => Assert.Throws<DomainValidationException>(() => new JobApplication(Job(), initialStatus: status));
    [Fact]
    public void BackwardMovesPreserveAppliedDateAndRestorePreservesOutcome()
    {
        var clock = new Clock(); var a = new JobApplication(Job(), clock); a.TransitionTo(ApplicationStatus.Applied, clock); var first = a.AppliedAtUtc;
        foreach (var stage in new[] { ApplicationStatus.Interview, ApplicationStatus.Offer, ApplicationStatus.Assessment, ApplicationStatus.Applied, ApplicationStatus.Offer, ApplicationStatus.Accepted, ApplicationStatus.Archived })
        { clock.Now = clock.Now.AddHours(1); a.TransitionTo(stage, clock); }
        Assert.Equal(first, a.AppliedAtUtc); a.Restore(a.StatusHistory.Last(), clock); Assert.Equal(ApplicationStatus.Accepted, a.Status);
        Assert.Throws<DomainValidationException>(() => a.TransitionTo(ApplicationStatus.Applied, clock));
        Assert.Equal(Enumerable.Range(0, a.StatusHistory.Count), a.StatusHistory.Select(h => h.Sequence));
    }
    [Fact]
    public void RestoreRejectsForeignOrStaleHistory()
    {
        var a = new JobApplication(Job()); a.TransitionTo(ApplicationStatus.Archived); var b = new JobApplication(Job()); b.TransitionTo(ApplicationStatus.Archived);
        Assert.Throws<DomainValidationException>(() => a.Restore(b.StatusHistory.Last()));
        a.Restore(a.StatusHistory.Last()); Assert.Equal(ApplicationStatus.Saved, a.Status);
        Assert.Throws<DomainValidationException>(() => a.Restore(a.StatusHistory.First()));
    }
    [Fact]
    public void AppliedDateCorrectionIsExplicitAndBounded()
    {
        var clock = new Clock(); var a = new JobApplication(Job(), clock);
        Assert.Throws<DomainValidationException>(() => a.CorrectAppliedDate(clock.Now.UtcDateTime));
        a.TransitionTo(ApplicationStatus.Applied, clock); var correction = clock.Now.AddDays(-4).UtcDateTime; a.CorrectAppliedDate(correction);
        Assert.Equal(correction, a.AppliedAtUtc);
        foreach (var bad in new DateTime?[] { null, clock.Now.AddDays(1).UtcDateTime, new(1899, 1, 1, 0, 0, 0, DateTimeKind.Utc), DateTime.SpecifyKind(correction, DateTimeKind.Unspecified) })
            Assert.Throws<DomainValidationException>(() => a.CorrectAppliedDate(bad));
        Assert.Equal(correction, a.AppliedAtUtc);
    }
    [Fact]
    public void NotesValidateAndPreservePlainText()
    {
        var note = new ApplicationNote(new JobApplication(Job()), " <script>text</script> "); Assert.Equal("<script>text</script>", note.Body);
        Assert.Throws<DomainValidationException>(() => note.Edit(" ")); Assert.Throws<DomainValidationException>(() => note.Edit(new string('a', 20001)));
        note.Edit(new string('a', 20000)); Assert.Equal(20000, note.Body.Length);
    }
    [Fact]
    public void QueriesRejectUnboundedAndUnknownValues()
    {
        foreach (var q in new[] { new ApplicationQuery(Page: 0), new ApplicationQuery(PageSize: 101), new ApplicationQuery(Status: "1"), new ApplicationQuery(Sort: "random"), new ApplicationQuery(State: "deleted"), new ApplicationQuery(Search: new string('x', 201)), new ApplicationQuery(AppliedFrom: DateTime.Now) })
            Assert.Throws<DomainValidationException>(q.Validate);
    }
}
