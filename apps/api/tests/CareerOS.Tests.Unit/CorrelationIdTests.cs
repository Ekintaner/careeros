using CareerOS.Api.Observability;
using Xunit;

namespace CareerOS.Tests.Unit;

public sealed class CorrelationIdTests
{
    [Fact]
    public void PreservesSafeCallerIdentifier() =>
        Assert.Equal("request-123_test.1", CorrelationId.Resolve("request-123_test.1"));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("unsafe\r\nheader")]
    [InlineData("two,values")]
    [InlineData("non-ascii-ü")]
    public void ReplacesUnsafeIdentifiers(string? value) =>
        Assert.True(Guid.TryParseExact(CorrelationId.Resolve(value), "N", out _));

    [Fact]
    public void ReplacesOversizedIdentifiers() =>
        Assert.True(Guid.TryParseExact(CorrelationId.Resolve(new string('a', 65)), "N", out _));
}
