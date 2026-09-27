using CareerOS.Application.Opportunities;
using CareerOS.Domain.Common;
using CareerOS.Domain.Opportunities;
using Xunit;

namespace CareerOS.Tests.Unit;

public sealed class OpportunityTests
{
    private static Company Company() => new(Guid.NewGuid(), "Example");
    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void CompanyNeedsAName(string name) => Assert.Throws<DomainValidationException>(() => new CompanyInput(name).Create(Guid.NewGuid()));
    [Theory]
    [InlineData("javascript:alert(1)")]
    [InlineData("https://user:password@example.com")]
    [InlineData("file:///etc/passwd")]
    [InlineData("https://example.com/ bad")]
    public void CompanyAndJobRejectUnsafeUrls(string url)
    {
        Assert.Throws<DomainValidationException>(() => new CompanyInput("Name", Website: url).Create(Guid.NewGuid()));
        Assert.Throws<DomainValidationException>(() => new JobInput(Guid.NewGuid(), "Role", SourceUrl: url).Create(Company()));
    }
    [Fact]
    public void InvalidCompanyEditLeavesOriginalUntouched()
    {
        var c = Company(); Assert.Throws<DomainValidationException>(() => new CompanyInput("Changed", Description: new string('x', 10001)).Apply(c)); Assert.Equal("Example", c.Name);
    }
    [Fact]
    public void InvalidJobEditsLeaveOriginalUntouched()
    {
        var c = Company(); var j = new Job(c, "Original");
        foreach (var input in new[] { new JobInput(c.Id, "Changed", SalaryMin: -1), new JobInput(c.Id, "Changed", WorkMode: "1"), new JobInput(c.Id, "Changed", Description: new string('x', 50001)) })
        { Assert.Throws<DomainValidationException>(() => input.Apply(j, c)); Assert.Equal("Original", j.Title); }
    }
    [Theory]
    [InlineData(-1, 10, "USD", "Year")]
    [InlineData(20, 10, "USD", "Year")]
    [InlineData(1.001, 10, "USD", "Year")]
    [InlineData(1, 10, "US", "Year")]
    [InlineData(1, 10, "USD", null)]
    public void SalaryRules(decimal min, decimal max, string currency, string? period) =>
        Assert.Throws<DomainValidationException>(() => new JobInput(Guid.NewGuid(), "Role", SalaryMin: min, SalaryMax: max, SalaryCurrency: currency, SalaryPeriod: period).Create(Company()));
    [Fact]
    public void PostingDatesRequireUtcAndOrderedWindow()
    {
        var date = DateTime.UtcNow;
        Assert.Throws<DomainValidationException>(() => new JobInput(Guid.NewGuid(), "Role", PostedAtUtc: date, ExpiresAtUtc: date.AddDays(-1)).Create(Company()));
        Assert.Throws<DomainValidationException>(() => new JobInput(Guid.NewGuid(), "Role", PostedAtUtc: DateTime.SpecifyKind(date, DateTimeKind.Unspecified)).Create(Company()));
    }
    [Fact]
    public void QueryRejectsUnboundedOrUnsupportedValues()
    {
        foreach (var q in new[] { new ListQuery(Page: 0), new ListQuery(Page: 10001), new ListQuery(PageSize: 101), new ListQuery(PageSize: 0), new ListQuery(Sort: "sql"), new ListQuery(WorkMode: "1"), new ListQuery(State: "deleted"), new ListQuery(Search: new string('x', 201)) })
            Assert.Throws<DomainValidationException>(() => q.Validate(true));
    }
    [Fact]
    public void ArchiveIsIdempotentAndRestorableAndCompanyCannotChangeOwner()
    {
        var j = new Job(Company(), "Role"); j.SetArchived(true, TimeProvider.System); var first = j.ArchivedAtUtc;
        j.SetArchived(true, TimeProvider.System); Assert.Equal(first, j.ArchivedAtUtc); j.SetArchived(false, TimeProvider.System); Assert.Null(j.ArchivedAtUtc);
        Assert.Throws<DomainValidationException>(() => j.ChangeCompany(Company()));
    }
}
