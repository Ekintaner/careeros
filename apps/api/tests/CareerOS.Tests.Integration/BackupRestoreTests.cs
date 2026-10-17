using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using CareerOS.Infrastructure.Documents;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace CareerOS.Tests.Integration;

public sealed class ContainerBackupFactAttribute : FactAttribute
{
    public ContainerBackupFactAttribute()
    {
        if (Environment.GetEnvironmentVariable("RUN_CONTAINER_TESTS") != "true" || !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("CAREEROS_TEST_POSTGRES")))
            Skip = "Synthetic dump/restore drill requires RUN_CONTAINER_TESTS=true without an external PostgreSQL server.";
    }
}

[Collection("PostgreSQL")]
public sealed class BackupRestoreTests(PostgreSqlFixture fixture)
{
    [ContainerBackupFact]
    public async Task PairedDumpAndPrivateBytesRestoreDownloadsAndOwnership()
    {
        await using var source = await fixture.CreateDatabaseAsync();
        await using var restored = await fixture.CreateDatabaseAsync();
        await using (var db = source.Context()) await db.Database.MigrateAsync();
        const string password = "synthetic backup drill phrase";
        var bytes = "%PDF-1.7\nSynthetic backup drill\n%%EOF\n"u8.ToArray();
        Dictionary<string, byte[]> files; Guid id;
        using (var h = new AuthFactory(source.ConnectionString))
        {
            using var a = h.Browser(); using var b = h.Browser();
            foreach (var (browser, email) in new[] { (a, "backup-a@example.com"), (b, "backup-b@example.com") })
                (await browser.Send("register", new { email, password, displayName = "Synthetic backup" })).EnsureSuccessStatusCode();
            using var body = new ByteArrayContent(bytes); body.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
            var response = await a.Api("documents?name=Synthetic&type=Resume&filename=synthetic.pdf", HttpMethod.Post, body);
            response.EnsureSuccessStatusCode(); id = (await response.Content.ReadFromJsonAsync<DocumentView>())!.Id;
            // All synthetic writes have finished. Capture both sides of the quiescent pair.
            await fixture.DumpRestoreAsync(source, restored);
            files = Directory.GetFiles(h.DocumentRoot).ToDictionary(path => Path.GetFileName(path), File.ReadAllBytes);
            Assert.Single(files);
        } // Original private files are removed by the test host; restoration cannot use them.
        using var host = new AuthFactory(restored.ConnectionString);
        Directory.CreateDirectory(host.DocumentRoot);
        foreach (var pair in files)
        {
            var path = Path.Combine(host.DocumentRoot, pair.Key); await File.WriteAllBytesAsync(path, pair.Value);
            Assert.Equal(SHA256.HashData(pair.Value), SHA256.HashData(await File.ReadAllBytesAsync(path)));
        }
        using var owner = host.Browser(); using var other = host.Browser();
        (await owner.Send("login", new { email = "backup-a@example.com", password })).EnsureSuccessStatusCode();
        (await other.Send("login", new { email = "backup-b@example.com", password })).EnsureSuccessStatusCode();
        var download = await owner.Api($"documents/{id}/download"); download.EnsureSuccessStatusCode();
        Assert.Equal(bytes, await download.Content.ReadAsByteArrayAsync());
        Assert.Equal(HttpStatusCode.NotFound, (await other.Api($"documents/{id}/download")).StatusCode);
        await using var check = restored.Context(); Assert.False(check.Database.HasPendingModelChanges());
        Assert.Equal(2, await check.Users.CountAsync()); Assert.Single(await check.Documents.ToArrayAsync()); Assert.Single(await check.Resumes.ToArrayAsync());
    }
}
