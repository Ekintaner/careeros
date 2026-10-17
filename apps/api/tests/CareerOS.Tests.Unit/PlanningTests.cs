using CareerOS.Application.Planning;
using CareerOS.Domain.Common;
using CareerOS.Domain.Planning;
using CareerOS.Domain.Applications;
using CareerOS.Domain.Opportunities;
using CareerOS.Domain.Notes;
using Xunit;
namespace CareerOS.Tests.Unit;
public sealed class PlanningTests
{
    [Theory]
    [InlineData(0, 20, null)]
    [InlineData(1, 101, null)]
    [InlineData(10001, 20, null)]
    [InlineData(1, 20, "unknown")]
    public void QueriesRejectInvalidBoundsAndViews(int page, int size, string? view)
    {
        Assert.Throws<DomainValidationException>(() => new PlanningQuery(page, size, View: view).Validate(true));
        Assert.Throws<DomainValidationException>(() => new PlanningQuery(page, size, View: view).Validate(false));
    }
    [Fact]
    public void ViewsAreExplicitAndModelHasTwoTaskStates()
    {
        foreach (var view in new[] { "open", "completed", "overdue", "upcoming", "all" }) new PlanningQuery(View: view).Validate(true);
        foreach (var view in new[] { "past", "upcoming", "all" }) new PlanningQuery(View: view).Validate(false);
        Assert.Throws<DomainValidationException>(() => new PlanningQuery(View: "completed").Validate(false));
        var task = new CareerTask(Guid.NewGuid(), "Prepare"); task.Complete(); var completed = task.CompletedAtUtc; task.Complete(); Assert.Equal(completed, task.CompletedAtUtc); task.Reopen(); Assert.Null(task.CompletedAtUtc);
    }
    [Fact]
    public void InterviewTypeAndNotesValidateWithoutCorruptingExistingValues()
    {
        var a = new JobApplication(new Job(new Company(Guid.NewGuid(), "Company"), "Role")); var start = DateTime.UtcNow;
        var interview = new Interview(a, InterviewType.Technical, start, start.AddHours(1));
        interview.ChangeType(InterviewType.Final); Assert.Throws<DomainValidationException>(() => interview.ChangeType((InterviewType)100)); Assert.Equal(InterviewType.Final, interview.Type);
        var note = new InterviewNote(interview, "Prepare examples"); Assert.Throws<DomainValidationException>(() => note.Edit(" ")); Assert.Equal("Prepare examples", note.Body);
        Assert.Throws<DomainValidationException>(() => interview.SetVenue("Remote", "javascript:alert(1)")); Assert.Null(interview.Location);
    }
}
