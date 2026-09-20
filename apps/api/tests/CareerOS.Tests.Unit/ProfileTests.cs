using CareerOS.Application.Profiles;
using CareerOS.Domain.Accounts;
using CareerOS.Domain.Common;
using CareerOS.Domain.Skills;
using Xunit;

namespace CareerOS.Tests.Unit;

public sealed class ProfileTests
{
    private static ProfileInput Input => new(" Engineer ", " Summary ", " Riga ", 2.5m, "Remote", [" Backend Developer ", "backend developer"], [" Riga "], ["FullTime"], ["Mid"], 1);
    [Fact]
    public void ProfileNormalizesPreferencesWithoutInventingPersonalData()
    {
        var p = new CareerProfile(Guid.NewGuid()); Input.Apply(p);
        Assert.Equal("Engineer", p.Headline); Assert.Equal("Riga", p.CurrentLocation);
        Assert.Equal(["Backend Developer"], p.TargetRoles); Assert.Equal(["FullTime"], p.EmploymentTypes);
        (Input with { Headline = " ", Summary = null, TargetRoles = [], PreferredLocations = [] }).Apply(p);
        Assert.Null(p.Headline); Assert.Empty(p.TargetRoles);
    }
    [Theory]
    [InlineData("headline")]
    [InlineData("summary")]
    [InlineData("location")]
    [InlineData("experience")]
    [InlineData("mode")]
    [InlineData("roles")]
    [InlineData("locations")]
    [InlineData("employment")]
    [InlineData("seniority")]
    [InlineData("null")]
    public void InvalidProfileCannotPartlyMutateAggregate(string field)
    {
        var p = new CareerProfile(Guid.NewGuid()); Input.Apply(p);
        var invalid = field switch
        {
            "headline" => Input with { Headline = new string('x', 201) },
            "summary" => Input with { Summary = new string('x', 10001) },
            "location" => Input with { CurrentLocation = new string('x', 201) },
            "experience" => Input with { YearsOfExperience = 1.25m },
            "mode" => Input with { PreferredWorkMode = "1" },
            "roles" => Input with { TargetRoles = Enumerable.Repeat("Role", 11).ToArray() },
            "locations" => Input with { PreferredLocations = [""] },
            "employment" => Input with { EmploymentTypes = ["invalid"] },
            "seniority" => Input with { SeniorityLevels = ["Junior"] },
            _ => Input with { TargetRoles = null! }
        };
        Assert.Throws<DomainValidationException>(() => invalid.Apply(p));
        Assert.Equal("Engineer", p.Headline); Assert.Equal(["Backend Developer"], p.TargetRoles);
    }
    [Theory]
    [InlineData(-1)]
    [InlineData(100.1)]
    [InlineData(1.01)]
    public void SkillExperienceRejectsOutOfRangeAndExcessPrecision(decimal experience)
    {
        var s = new UserSkill(Guid.NewGuid(), Guid.NewGuid());
        Assert.Throws<DomainValidationException>(() => ProfileValidation.Assess(s, "Advanced", experience));
        Assert.Null(s.Proficiency);
    }
    [Fact]
    public void SkillCanonicalizationAndMetadataUseExistingRules()
    {
        Assert.Equal(new Skill(" PostgreSQL ").NormalizedName, new Skill("POSTGRESQL").NormalizedName);
        Assert.Throws<DomainValidationException>(() => new Skill("PostgréSQL"));
        var s = new UserSkill(Guid.NewGuid(), Guid.NewGuid());
        Assert.Throws<DomainValidationException>(() => ProfileValidation.Assess(s, "3", 1));
        ProfileValidation.Assess(s, "Advanced", 2.5m); Assert.Equal(Proficiency.Advanced, s.Proficiency);
        ProfileValidation.Assess(s, null, null); Assert.Null(s.YearsExperience);
    }
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void AccountRenameRejectsBlankNames(string name)
    {
        var user = new User("unit@example.com", "Before");
        Assert.Throws<DomainValidationException>(() => user.Rename(name));
        Assert.Throws<DomainValidationException>(() => user.Rename(new string('x', 121)));
        user.Rename(" After "); Assert.Equal("After", user.DisplayName);
    }
}
