using Shora.Api.Middleware;

namespace Shora.Tests.Unit.Middleware;

public class CorrelationIdMiddlewareTests
{
    [Theory]
    [InlineData("test-correlation-id-12345")]
    [InlineData("a1b2c3d4-e5f6-7890-abcd-ef1234567890")]
    [InlineData("a1b2c3d4e5f67890abcdef1234567890")]
    [InlineData("trace_01-ABC")]
    public void IsValidCorrelationId_accepts_safe_values(string value)
    {
        Assert.True(CorrelationIdMiddleware.IsValidCorrelationId(value));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("<script>alert(1)</script>")]
    [InlineData("id with spaces")]
    [InlineData("id;drop table")]
    public void IsValidCorrelationId_rejects_unsafe_values(string? value)
    {
        Assert.False(CorrelationIdMiddleware.IsValidCorrelationId(value));
    }

    [Fact]
    public void IsValidCorrelationId_rejects_values_longer_than_64_characters()
    {
        var value = new string('a', 65);

        Assert.False(CorrelationIdMiddleware.IsValidCorrelationId(value));
    }

    [Fact]
    public void ResolveCorrelationId_returns_incoming_value_when_valid()
    {
        const string incoming = "client-trace-001";

        var resolved = CorrelationIdMiddleware.ResolveCorrelationId(incoming);

        Assert.Equal(incoming, resolved);
    }

    [Fact]
    public void ResolveCorrelationId_generates_new_id_when_incoming_is_invalid()
    {
        var resolved = CorrelationIdMiddleware.ResolveCorrelationId("not valid!");

        Assert.NotEqual("not valid!", resolved);
        Assert.True(CorrelationIdMiddleware.IsValidCorrelationId(resolved));
        Assert.Equal(32, resolved.Length);
    }
}
