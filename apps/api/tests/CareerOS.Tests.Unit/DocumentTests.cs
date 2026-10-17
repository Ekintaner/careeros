using System.Text;
using CareerOS.Domain.Common;
using CareerOS.Infrastructure.Documents;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace CareerOS.Tests.Unit;
public sealed class DocumentTests
{
    [Theory]
    [InlineData("../../resume.pdf", "resume.pdf")]
    [InlineData("C:\\private\\my cv.pdf", "my cv.pdf")]
    [InlineData("evil\r\n.pdf", "evil__.pdf")]
    public void FilenamesAreSafe(string input, string expected) => Assert.Equal(expected, LocalDocumentStore.SafeName(input));
    [Theory]
    [InlineData("resume.exe")]
    [InlineData("resume.pdf.exe")]
    [InlineData("")]
    public void UnsupportedNamesRejected(string input) => Assert.Throws<DomainValidationException>(() => LocalDocumentStore.SafeName(input));
    [Theory]
    [InlineData("text/html", "%PDF-1.7\nbody\n%%EOF")]
    [InlineData("application/pdf", "<html>not a PDF</html>")]
    [InlineData("application/pdf", "%PDF-1.7 missing trailer")]
    public async Task InvalidFilesRejected(string mime, string data) => await Assert.ThrowsAsync<DomainValidationException>(() => LocalDocumentStore.Validate(new MemoryStream(Encoding.ASCII.GetBytes(data)), mime, default));
    [Fact]
    public async Task OversizeStreamRejected() => await Assert.ThrowsAsync<DocumentTooLargeException>(() => LocalDocumentStore.Validate(new MemoryStream(new byte[LocalDocumentStore.MaxBytes + 1]), "application/pdf", default));
    [Fact]
    public async Task ExactLimitAcceptedAndCancelledUploadDoesNotContinueReading()
    {
        var bytes = Enumerable.Repeat((byte)' ', LocalDocumentStore.MaxBytes).ToArray();
        "%PDF-1.7\n"u8.CopyTo(bytes); "\n%%EOF"u8.CopyTo(bytes.AsSpan(bytes.Length - 6));
        Assert.Equal(bytes, await LocalDocumentStore.Validate(new MemoryStream(bytes), "application/pdf", default));
        using var cancelled = new CancellationTokenSource(); cancelled.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => LocalDocumentStore.Validate(new MemoryStream(bytes), "application/pdf", cancelled.Token));
    }
    [Fact]
    public void UnsafeStorageLocationsRejected()
    {
        var root = Path.Combine(Path.GetTempPath(), "careeros-location-test-" + Guid.NewGuid().ToString("N"));
        IConfiguration Config(string path) => new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["Documents:StorageRoot"] = path }).Build();
        Assert.Throws<InvalidOperationException>(() => new LocalDocumentStore(Config("relative-storage")));
        try
        {
            Directory.CreateDirectory(Path.Combine(root, ".git"));
            Assert.Throws<InvalidOperationException>(() => new LocalDocumentStore(Config(Path.Combine(root, "files"))));
            Assert.Throws<InvalidOperationException>(() => new LocalDocumentStore(Config(Path.Combine(root, "wwwroot", "files")), root));
        }
        finally { Directory.Delete(root, true); }
    }
    [Fact]
    public void LinkedStorageRejectedOnUnix()
    {
        if (OperatingSystem.IsWindows()) return; // Windows symbolic links require optional OS privileges; CI exercises this on Linux.
        var root = Path.Combine(Path.GetTempPath(), "careeros-link-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var target = Path.Combine(root, "target"); Directory.CreateDirectory(target);
        var link = Path.Combine(root, "linked"); Directory.CreateSymbolicLink(link, target);
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["Documents:StorageRoot"] = link }).Build();
        try { Assert.Throws<IOException>(() => new LocalDocumentStore(config)); }
        finally { Directory.Delete(link); Directory.Delete(root, true); }
    }
    [Fact]
    public async Task StoreRoundTripsAndRejectsTraversal()
    {
        var root = Path.Combine(Path.GetTempPath(), "careeros-store-test-" + Guid.NewGuid().ToString("N"));
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["Documents:StorageRoot"] = root }).Build();
        try
        {
            var store = new LocalDocumentStore(config);
            foreach (var key in new[] { "../secret.pdf", "/etc/passwd", "abc.pdf", "..\\x.pdf" }) Assert.Throws<FileNotFoundException>(() => store.PathFor(key));
            var bytes = Encoding.ASCII.GetBytes("%PDF-1.7\nhello\n%%EOF\n");
            var key2 = await store.Write(bytes, default);
            await using (var file = store.Open(key2)) { using var result = new MemoryStream(); await file.CopyToAsync(result); Assert.Equal(bytes, result.ToArray()); }
            store.Delete(key2); Assert.False(File.Exists(store.PathFor(key2)));
        }
        finally { Directory.Delete(root, true); }
    }
}
