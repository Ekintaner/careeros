using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using CareerOS.Application.Applications;
using CareerOS.Application.Opportunities;
using CareerOS.Infrastructure.Documents;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace CareerOS.Tests.Integration;
[Collection("PostgreSQL")]
public sealed class DocumentTests(PostgreSqlFixture fixture)
{
    private static readonly byte[] Pdf = Encoding.ASCII.GetBytes("%PDF-1.7\n1 0 obj <</Type /Catalog>> endobj\n%%EOF\n");
    private static async Task<T> Read<T>(HttpResponseMessage r) { r.EnsureSuccessStatusCode(); return (await r.Content.ReadFromJsonAsync<T>())!; }
    private static async Task Register(Browser b, string email) => (await b.Send("register", new { email, password = "document tests passphrase", displayName = "Test" })).EnsureSuccessStatusCode();
    private static Task<HttpResponseMessage> Upload(Browser b, string name = "Resume", string type = "Resume", byte[]? bytes = null, string mime = "application/pdf", string filename = "resume.pdf", bool csrf = true)
    {
        var body = new ByteArrayContent(bytes ?? Pdf); body.Headers.ContentType = new MediaTypeHeaderValue(mime);
        return b.Api($"documents?name={Uri.EscapeDataString(name)}&type={type}&filename={Uri.EscapeDataString(filename)}", HttpMethod.Post, body, csrf);
    }
    [PostgreSqlFact]
    public async Task OwnerIsolationUploadDownloadMetadataAndDeletion()
    {
        await using var d = await fixture.CreateDatabaseAsync(); await using (var db = d.Context()) await db.Database.MigrateAsync();
        using var h = new AuthFactory(d.ConnectionString); using var a = h.Browser(); using var b = h.Browser(); using var anonymous = h.Browser();
        await Register(a, "docs-a@example.com"); await Register(b, "docs-b@example.com");
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.Api("documents")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await Upload(anonymous)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await Upload(a, csrf: false)).StatusCode);
        var file = await Read<DocumentView>(await Upload(a, filename: "../../resume.pdf"));
        Assert.Equal("resume.pdf", file.OriginalFileName); Assert.True(file.IsPrimary); Assert.NotNull(file.ResumeId); Assert.Equal(Pdf.Length, file.FileSizeBytes);
        var response = await a.Api($"documents/{file.Id}/download"); response.EnsureSuccessStatusCode();
        Assert.Equal(Pdf, await response.Content.ReadAsByteArrayAsync()); Assert.Equal("attachment", response.Content.Headers.ContentDisposition?.DispositionType); Assert.Equal("no-store", response.Headers.CacheControl?.ToString());
        var list = await a.Api("documents"); var json = await list.Content.ReadAsStringAsync(); Assert.DoesNotContain("storageKey", json, StringComparison.OrdinalIgnoreCase); Assert.DoesNotContain(h.DocumentRoot, json);
        Assert.Single((await Read<Page<DocumentView>>(list)).Items);
        Assert.Empty((await Read<Page<DocumentView>>(await b.Api("documents"))).Items);
        Assert.Equal(HttpStatusCode.NotFound, (await b.Api($"documents/{file.Id}/download")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await b.Api($"documents/{file.Id}", HttpMethod.Delete)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await b.Api($"documents/resumes/{file.ResumeId}/primary", HttpMethod.Post)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await a.Api($"documents/{file.Id}", HttpMethod.Delete)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await a.Api($"documents/{file.Id}/download")).StatusCode);
        Assert.Empty(Directory.GetFiles(h.DocumentRoot));
        await using var reopened = d.Context(); Assert.Empty(await reopened.Documents.ToListAsync()); Assert.Empty(await reopened.Resumes.ToListAsync());
    }
    [PostgreSqlFact]
    public async Task ValidationAndFailureLeaveNoRowsOrBytes()
    {
        await using var d = await fixture.CreateDatabaseAsync(); await using (var db = d.Context()) await db.Database.MigrateAsync();
        using var h = new AuthFactory(d.ConnectionString); using var a = h.Browser(); await Register(a, "invalid-docs@example.com");
        foreach (var response in new[] { await Upload(a, mime: "text/html"), await Upload(a, bytes: "<html>invalid</html>"u8.ToArray()), await Upload(a, bytes: []), await Upload(a, filename: "cv.pdf.exe"), await Upload(a, type: "1"), await Upload(a, name: " ") }) Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, (await Upload(a, bytes: new byte[LocalDocumentStore.MaxBytes + 1])).StatusCode);
        Assert.Empty(Directory.GetFiles(h.DocumentRoot)); await using var reopened = d.Context(); Assert.Empty(await reopened.Documents.ToListAsync());
        Assert.Equal(HttpStatusCode.BadRequest, (await a.Api("documents?page=0")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await a.Api("documents?type=1")).StatusCode);
    }
    [PostgreSqlFact]
    public async Task DatabaseFailuresPreserveConsistencyAndMissingFilesReturn404()
    {
        await using var d = await fixture.CreateDatabaseAsync(); await using (var db = d.Context()) await db.Database.MigrateAsync();
        using var h = new AuthFactory(d.ConnectionString); using var a = h.Browser(); await Register(a, "rollback-docs@example.com");
        await d.ExecuteAsync("CREATE FUNCTION reject_document() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN RAISE EXCEPTION 'simulated write failure'; END; $$; CREATE TRIGGER reject_document BEFORE INSERT ON documents FOR EACH ROW EXECUTE FUNCTION reject_document();");
        Assert.Equal(HttpStatusCode.InternalServerError, (await Upload(a)).StatusCode);
        Assert.Empty(Directory.GetFiles(h.DocumentRoot));
        await using (var db = d.Context()) Assert.Empty(await db.Documents.ToListAsync());
        await d.ExecuteAsync("DROP TRIGGER reject_document ON documents;");
        var document = await Read<DocumentView>(await Upload(a));
        await d.ExecuteAsync("CREATE TRIGGER reject_document BEFORE DELETE ON documents FOR EACH ROW EXECUTE FUNCTION reject_document();");
        Assert.Equal(HttpStatusCode.InternalServerError, (await a.Api($"documents/{document.Id}", HttpMethod.Delete)).StatusCode);
        Assert.Equal(Pdf, await (await a.Api($"documents/{document.Id}/download")).Content.ReadAsByteArrayAsync());
        await using (var db = d.Context()) { Assert.Single(await db.Documents.ToListAsync()); Assert.Single(await db.Resumes.ToListAsync()); }
        File.Delete(Assert.Single(Directory.GetFiles(h.DocumentRoot)));
        Assert.Equal(HttpStatusCode.NotFound, (await a.Api($"documents/{document.Id}/download")).StatusCode);
    }
    [PostgreSqlFact]
    public async Task PrimaryConcurrencyAssignmentRestrictionsAndFilters()
    {
        await using var d = await fixture.CreateDatabaseAsync(); await using (var db = d.Context()) await db.Database.MigrateAsync();
        using var h = new AuthFactory(d.ConnectionString); using var a = h.Browser(); using var b = h.Browser(); await Register(a, "primary-a@example.com"); await Register(b, "primary-b@example.com");
        var uploads = await Task.WhenAll(Upload(a, "One"), Upload(a, "Two")); var one = await Read<DocumentView>(uploads[0]); var two = await Read<DocumentView>(uploads[1]);
        Assert.Single((await Read<Page<DocumentView>>(await a.Api("documents?type=Resume"))).Items, x => x.IsPrimary);
        var changes = await Task.WhenAll(a.Api($"documents/resumes/{one.ResumeId}/primary", HttpMethod.Post), a.Api($"documents/resumes/{two.ResumeId}/primary", HttpMethod.Post));
        Assert.All(changes, x => Assert.Equal(HttpStatusCode.NoContent, x.StatusCode));
        Assert.Single((await Read<Page<DocumentView>>(await a.Api("documents"))).Items, x => x.IsPrimary);
        await Read<DocumentView>(await Upload(a, "Certificate", "Certificate"));
        Assert.Single((await Read<Page<DocumentView>>(await a.Api("documents?type=Certificate"))).Items);
        var c = await Read<CompanyView>(await a.Api("companies", HttpMethod.Post, new CompanyInput("Company")));
        var j = await Read<JobView>(await a.Api("jobs", HttpMethod.Post, new JobInput(c.Id, "Job")));
        var foreign = await Read<DocumentView>(await Upload(b));
        Assert.Equal(HttpStatusCode.NotFound, (await a.Api("applications", HttpMethod.Post, new CreateApplicationInput(j.Id, ResumeId: foreign.ResumeId))).StatusCode);
        var app = await Read<ApplicationView>(await a.Api("applications", HttpMethod.Post, new CreateApplicationInput(j.Id, ResumeId: one.ResumeId)));
        Assert.Equal(one.ResumeId, app.ResumeId);
        Assert.Equal(HttpStatusCode.Conflict, (await a.Api($"documents/{one.Id}", HttpMethod.Delete)).StatusCode);
        Assert.Equal(Pdf, await (await a.Api($"documents/{one.Id}/download")).Content.ReadAsByteArrayAsync());
        app = await Read<ApplicationView>(await a.Api($"applications/{app.Id}/archive", HttpMethod.Post, new VersionInput(app.Version)));
        Assert.Equal(HttpStatusCode.Conflict, (await a.Api($"documents/{one.Id}", HttpMethod.Delete)).StatusCode);
        await Read<ApplicationView>(await a.Api($"applications/{app.Id}", HttpMethod.Put, new EditApplicationInput(app.Version, ResumeId: null)));
        Assert.Equal(HttpStatusCode.NoContent, (await a.Api($"documents/{one.Id}", HttpMethod.Delete)).StatusCode);
    }
}
