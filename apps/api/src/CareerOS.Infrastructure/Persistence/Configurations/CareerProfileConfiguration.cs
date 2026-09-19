using CareerOS.Domain.Accounts;
using CareerOS.Domain.Opportunities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CareerOS.Infrastructure.Persistence.Configurations;

internal sealed class CareerProfileConfiguration : IEntityTypeConfiguration<CareerProfile>
{
    public void Configure(EntityTypeBuilder<CareerProfile> b)
    {
        b.ToTable("career_profiles");
        b.HasKey(x => x.UserId);
        b.Property(x => x.UserId).ValueGeneratedNever();
        Mapping.Owner(b);
        Mapping.Audit(b);
        Mapping.Text(b, nameof(CareerProfile.Headline), 200);
        Mapping.Text(b, nameof(CareerProfile.Summary), 10000);
        Mapping.Text(b, nameof(CareerProfile.CurrentLocation), 200);
        b.Property(x => x.YearsOfExperience).HasPrecision(4, 1);
        Mapping.Check(b, "experience_range", "\"YearsOfExperience\" BETWEEN 0 AND 100");
        Mapping.Enum<CareerProfile, WorkMode>(b, nameof(CareerProfile.PreferredWorkMode), true);
    }
}
