using CareerOS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Testcontainers.PostgreSql;
using Xunit;

namespace CareerOS.Tests.Integration;

[CollectionDefinition("PostgreSQL")]
public sealed class PostgreSqlCollection : ICollectionFixture<PostgreSqlFixture>;

public sealed class PostgreSqlFixture : IAsyncLifetime
{
    private PostgreSqlContainer? container;
    private string connectionString = "";
    internal static bool Enabled => Environment.GetEnvironmentVariable("RUN_CONTAINER_TESTS") == "true"
        || !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("CAREEROS_TEST_POSTGRES"));

    public async Task InitializeAsync()
    {
        if (!Enabled) return;
        var external = Environment.GetEnvironmentVariable("CAREEROS_TEST_POSTGRES");
        if (!string.IsNullOrWhiteSpace(external)) connectionString = external;
        else
        {
            container = new PostgreSqlBuilder("postgres:17-alpine").Build();
            await container.StartAsync();
            connectionString = container.GetConnectionString();
        }
    }

    public async Task<TestDatabase> CreateDatabaseAsync()
    {
        var name = "careeros_test_" + Guid.NewGuid().ToString("N");
        var admin = new NpgsqlConnectionStringBuilder(connectionString) { Database = "postgres", Pooling = false }.ConnectionString;
        await using var connection = new NpgsqlConnection(admin);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand($"CREATE DATABASE \"{name}\"", connection);
        await command.ExecuteNonQueryAsync();
        return new TestDatabase(admin, new NpgsqlConnectionStringBuilder(connectionString) { Database = name, Pooling = false }.ConnectionString, name);
    }

    public async Task DisposeAsync()
    {
        if (container is not null) await container.DisposeAsync();
    }

    internal async Task DumpRestoreAsync(TestDatabase source, TestDatabase restored)
    {
        if (container is null) throw new InvalidOperationException("Backup drill requires the disposable Testcontainers server.");
        var user = new NpgsqlConnectionStringBuilder(connectionString).Username!;
        var file = "/tmp/" + source.DatabaseName + ".dump";
        var dump = await container.ExecAsync(["pg_dump", "-U", user, "-d", source.DatabaseName, "-Fc", "-f", file]);
        if (dump.ExitCode != 0) throw new InvalidOperationException("Synthetic pg_dump failed.");
        var restore = await container.ExecAsync(["pg_restore", "-U", user, "-d", restored.DatabaseName, "--exit-on-error", "--no-owner", "--no-acl", file]);
        if (restore.ExitCode != 0) throw new InvalidOperationException("Synthetic pg_restore failed.");
    }
}

public sealed class TestDatabase(string adminConnection, string connectionString, string databaseName) : IAsyncDisposable
{
    internal string DatabaseName => databaseName;
    public string ConnectionString => connectionString;
    public CareerOSDbContext Context(TimeProvider? clock = null) => new(
        new DbContextOptionsBuilder<CareerOSDbContext>().UseNpgsql(connectionString).Options, clock);

    public async Task<int> ExecuteAsync(string sql)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        return await command.ExecuteNonQueryAsync();
    }

    public async Task<T> ScalarAsync<T>(string sql)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        return (T)(await command.ExecuteScalarAsync())!;
    }

    public async ValueTask DisposeAsync()
    {
        // Only the randomly named database created by this fixture is removed.
        if (!databaseName.StartsWith("careeros_test_", StringComparison.Ordinal) || databaseName.Length != 46)
            throw new InvalidOperationException("Refusing to remove a database not owned by this test fixture.");
        await using var connection = new NpgsqlConnection(adminConnection);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand($"DROP DATABASE \"{databaseName}\" WITH (FORCE)", connection);
        await command.ExecuteNonQueryAsync();
    }
}

public sealed class PostgreSqlFactAttribute : FactAttribute
{
    public PostgreSqlFactAttribute()
    {
        if (!PostgreSqlFixture.Enabled) Skip = "Enable RUN_CONTAINER_TESTS with Docker or provide CAREEROS_TEST_POSTGRES for a disposable PostgreSQL server.";
    }
}

public sealed class PostgreSqlTheoryAttribute : TheoryAttribute
{
    public PostgreSqlTheoryAttribute()
    {
        if (!PostgreSqlFixture.Enabled) Skip = "Enable RUN_CONTAINER_TESTS with Docker or provide CAREEROS_TEST_POSTGRES for a disposable PostgreSQL server.";
    }
}
