using CareerOS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;
using Xunit;

namespace CareerOS.Tests.Integration;

public sealed class PostgreSqlTests
{
    [DockerFact]
    [Trait("Category", "Docker")]
    public async Task DbContextConnectsToRealPostgreSql()
    {
        await using var database = new PostgreSqlBuilder("postgres:17-alpine").Build();
        await database.StartAsync();
        var options = new DbContextOptionsBuilder<CareerOSDbContext>().UseNpgsql(database.GetConnectionString()).Options;
        await using var context = new CareerOSDbContext(options);
        Assert.True(await context.Database.CanConnectAsync());
        Assert.Empty(context.Model.GetEntityTypes());
    }
}

public sealed class DockerFactAttribute : FactAttribute
{
    public DockerFactAttribute()
    {
        if (!string.Equals(Environment.GetEnvironmentVariable("RUN_CONTAINER_TESTS"), "true", StringComparison.OrdinalIgnoreCase))
            Skip = "Set RUN_CONTAINER_TESTS=true with Docker running to test real PostgreSQL.";
    }
}
