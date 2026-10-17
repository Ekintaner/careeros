using CareerOS.Application.Intelligence;
using Xunit;
using CareerOS.Domain.Common;
namespace CareerOS.Tests.Unit;
public sealed class IntelligenceTests
{
    [Theory]
    [InlineData(true, 1, "supported", "Maintain")]
    [InlineData(true, 0, "evidenceGap", "Document resume evidence")]
    [InlineData(false, 1, "evidenceGap", "Review profile record")]
    [InlineData(false, 0, "missing", "Learn or assess")]
    public void EvidenceAndLearningAreSeparate(bool profile, int resumes, string status, string action)
    {
        var skill = IntelligenceRules.Skill(Guid.NewGuid(), "SQL", 1, 1, profile, resumes, 2);
        Assert.Equal(status, skill.Status); Assert.Equal(action, skill.Action); Assert.Equal(4, skill.WeightedDemand);
        Assert.Equal(100m, skill.DemandPercent); Assert.Equal(status == "supported" ? 0m : 66.7m, skill.ActionPriority);
        Assert.Equal(status == "missing" ? 66.7m : 0m, skill.LearningPriority);
    }
    [Fact]
    public void PreferredOnlyPriorityAndNoAnalyzedJobsAreExplicit()
    {
        Assert.Equal(33.3m, IntelligenceRules.Skill(Guid.NewGuid(), "SQL", 0, 2, false, 0, 2).LearningPriority);
        var empty = IntelligenceRules.Skill(Guid.NewGuid(), "SQL", 0, 0, false, 0, 0);
        Assert.Equal(0m, empty.DemandPercent); Assert.Equal(0m, empty.ActionPriority);
    }
    [Theory]
    [InlineData(0, 20, "active", "all", "priority")]
    [InlineData(1, 51, "active", "all", "priority")]
    [InlineData(1, 20, "deleted", "all", "priority")]
    [InlineData(1, 20, "active", "unknown", "priority")]
    [InlineData(1, 20, "active", "all", "sql")]
    public void QueryRejectsUnboundedOrUnknownValues(int page, int size, string state, string status, string sort) =>
        Assert.Throws<DomainValidationException>(() => new IntelligenceQuery(page, size, state, status, sort).Validate());
}
