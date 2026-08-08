using System.Text;
using Microsoft.AspNetCore.Http;
using Shora.Api.Infrastructure;
using Shora.Api.Middleware;

namespace Shora.Tests.Unit.Middleware;

public class AuthRateLimitEmailMiddlewareTests
{
    [Fact]
    public async Task InvokeAsync_extracts_normalized_email_and_preserves_json_body()
    {
        const string payload = "{\"email\":\"  Mixed.Case@Example.COM  \",\"password\":\"Password123!\"}";
        var context = CreateJsonContext(payload, includeExtractionMetadata: true);
        string? capturedEmail = null;
        string? downstreamBody = null;

        var middleware = new AuthRateLimitEmailMiddleware(async nextContext =>
        {
            capturedEmail = AuthRateLimitEmailMiddleware.TryGetAuthEmail(nextContext);
            downstreamBody = await ReadBodyAsync(nextContext.Request);
        });

        await middleware.InvokeAsync(context);

        Assert.Equal("mixed.case@example.com", capturedEmail);
        Assert.Equal(payload, downstreamBody);
    }

    [Fact]
    public async Task InvokeAsync_preserves_body_when_json_is_malformed()
    {
        const string payload = "{\"email\":\"client@example.com\"";
        var context = CreateJsonContext(payload, includeExtractionMetadata: true);
        string? capturedEmail = "not-cleared";
        string? downstreamBody = null;

        var middleware = new AuthRateLimitEmailMiddleware(async nextContext =>
        {
            capturedEmail = AuthRateLimitEmailMiddleware.TryGetAuthEmail(nextContext);
            downstreamBody = await ReadBodyAsync(nextContext.Request);
        });

        await middleware.InvokeAsync(context);

        Assert.Null(capturedEmail);
        Assert.Equal(payload, downstreamBody);
    }

    [Fact]
    public async Task InvokeAsync_skips_extraction_without_endpoint_metadata()
    {
        const string payload = "{\"email\":\"client@example.com\"}";
        var context = CreateJsonContext(payload, includeExtractionMetadata: false);
        string? capturedEmail = "not-cleared";
        string? downstreamBody = null;

        var middleware = new AuthRateLimitEmailMiddleware(async nextContext =>
        {
            capturedEmail = AuthRateLimitEmailMiddleware.TryGetAuthEmail(nextContext);
            downstreamBody = await ReadBodyAsync(nextContext.Request);
        });

        await middleware.InvokeAsync(context);

        Assert.Null(capturedEmail);
        Assert.Equal(payload, downstreamBody);
    }

    private static DefaultHttpContext CreateJsonContext(string payload, bool includeExtractionMetadata)
    {
        var context = new DefaultHttpContext();
        context.Request.ContentType = "application/json; charset=utf-8";
        context.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(payload));

        if (includeExtractionMetadata)
        {
            context.SetEndpoint(new Endpoint(
                _ => Task.CompletedTask,
                new EndpointMetadataCollection(new ExtractAuthEmailForRateLimitAttribute()),
                "Auth test endpoint"));
        }

        return context;
    }

    private static async Task<string> ReadBodyAsync(HttpRequest request)
    {
        using var reader = new StreamReader(
            request.Body,
            Encoding.UTF8,
            detectEncodingFromByteOrderMarks: false,
            leaveOpen: true);
        return await reader.ReadToEndAsync();
    }
}
