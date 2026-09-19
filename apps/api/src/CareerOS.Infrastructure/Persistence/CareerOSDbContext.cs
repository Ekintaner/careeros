using Microsoft.EntityFrameworkCore;

namespace CareerOS.Infrastructure.Persistence;

public sealed class CareerOSDbContext(DbContextOptions<CareerOSDbContext> options) : DbContext(options)
{
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(CareerOSDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }
}
