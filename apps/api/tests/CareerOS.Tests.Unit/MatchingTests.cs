using CareerOS.Application.Matching;
using CareerOS.Domain.Common;
using Xunit;

namespace CareerOS.Tests.Unit;
public sealed class MatchingTests
{
    private static readonly Guid A = Guid.Parse("00000000-0000-0000-0000-000000000001");
    private static readonly Guid B = Guid.Parse("00000000-0000-0000-0000-000000000002");
    private static readonly DateTime Recorded = new(2026, 10, 17, 0, 0, 0, DateTimeKind.Utc);
    [Fact]
    public void ScoreExplainsWeightedCoverageAndEvidence()
    {
        var result = MatchingRules.Calculate([new(A, "C#", "Required"), new(B, "SQL", "Preferred")], [new(A, "C#", Recorded)]);
        Assert.Equal(75m, result.Score);
        Assert.Equal(new MatchBreakdown(1, 1, 3, 3), result.Required);
        Assert.Equal(new MatchBreakdown(0, 1, 0, 1), result.Preferred);
        Assert.Equal(Recorded, result.Skills[0].EvidenceRecordedAtUtc);
        Assert.Null(result.Skills[1].EvidenceRecordedAtUtc);
        Assert.False(result.Skills[1].Matched);
        Assert.Equal("skills-v1", result.Algorithm);
    }
    [Fact]
    public void NoRequirementsIsUnknownAndNoEvidenceIsZero()
    {
        Assert.Null(MatchingRules.Calculate([], [new(A, "C#", Recorded)]).Score);
        Assert.Equal(0m, MatchingRules.Calculate([new(A, "C#", "Required")], []).Score);
    }
    [Fact]
    public void PreferredOnlyAndUnrelatedEvidenceRemainHonest()
    {
        Assert.Equal(100m, MatchingRules.Calculate([new(A, "C++", "Preferred")], [new(A, "C++", Recorded), new(B, "SQL", Recorded)]).Score);
        Assert.Equal(0m, MatchingRules.Calculate([new(A, "C++", "Preferred")], [new(B, "C", Recorded)]).Score);
    }
    [Fact]
    public void OrderingAndRoundingAreDeterministic()
    {
        MatchRequirement[] requirements = [new(B, "SQL", "Required"), new(A, "C#", "Required"), new(Guid.NewGuid(), ".NET", "Required")];
        var result = MatchingRules.Calculate(requirements, [new(A, "C#", Recorded)]);
        Assert.Equal(33.3m, result.Score);
        Assert.Equal(result.Skills, MatchingRules.Calculate(requirements.Reverse(), [new(A, "C#", Recorded)]).Skills);
    }
    [Theory]
    [InlineData("Required")]
    [InlineData("Preferred")]
    public void ValidationTrimsNamesPreservingSignificantPunctuation(string level)
    {
        Assert.Equal("C++", Assert.Single(MatchingRules.Validate(new JobSkillsInput(1, [new(" C++ ", level)]))).Name);
        Assert.Equal(["C#", ".NET"], MatchingRules.Validate(new ResumeSkillsInput(1, [" C# ", ".NET"])));
    }
    [Fact]
    public void ValidationRejectsDuplicatesBoundsUnknownEnumsAndNullEntries()
    {
        Assert.Throws<DomainValidationException>(() => MatchingRules.Validate(new JobSkillsInput(1, [new("SQL", "Required"), new(" sql ", "Preferred")])));
        Assert.Throws<DomainValidationException>(() => MatchingRules.Validate(new JobSkillsInput(1, [new("SQL", "0")])));
        Assert.Throws<DomainValidationException>(() => MatchingRules.Validate(new JobSkillsInput(1, [null!])));
        Assert.Throws<DomainValidationException>(() => MatchingRules.Validate(new JobSkillsInput(0, [])));
        Assert.Throws<DomainValidationException>(() => MatchingRules.Validate(new ResumeSkillsInput(1, null!)));
        Assert.Throws<DomainValidationException>(() => MatchingRules.Validate(new ResumeSkillsInput(1, ["SQL", "sql"])));
        Assert.Throws<DomainValidationException>(() => MatchingRules.Validate(new ResumeSkillsInput(1, Enumerable.Range(0, 101).Select(x => "Skill" + x).ToArray())));
        foreach (var name in new[] { "", " ", "SQL\n", "ş", new string('a', 101), "SQL\tserver" })
        {
            // Trailing whitespace is trimmed by canonical validation; internal controls remain invalid.
            if (name == "SQL\n") continue;
            Assert.Throws<DomainValidationException>(() => MatchingRules.Validate(new ResumeSkillsInput(1, [name])));
        }
    }
}
