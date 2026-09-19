using CareerOS.Domain.Opportunities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CareerOS.Infrastructure.Persistence.Configurations;

internal sealed class CompanyConfiguration : IEntityTypeConfiguration<Company>
{
    public void Configure(EntityTypeBuilder<Company> b)
    {
        b.ToTable("companies");
        Mapping.Identity(b);
        Mapping.Owner(b, true);
        Mapping.Audit(b);
        Mapping.Text(b, nameof(Company.Name), 200, true);
        Mapping.Url(b, nameof(Company.Website));
        Mapping.Text(b, nameof(Company.Industry), 120);
        Mapping.Text(b, nameof(Company.Location), 200);
        Mapping.Text(b, nameof(Company.Description), 10000);
        b.HasIndex(x => new { x.UserId, x.Name });
    }
}
