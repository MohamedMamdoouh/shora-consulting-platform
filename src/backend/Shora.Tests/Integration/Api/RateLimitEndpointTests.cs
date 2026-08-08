using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Shora.Contracts.Auth;
using Shora.Tests.Common;

namespace Shora.Tests.Integration.Api;

[Collection("SqlServer")]
public class RateLimitEndpointTests : IDisposable
{
    private readonly AuthWebApplicationFactory _factory;

    public RateLimitEndpointTests(SqlServerFixture sqlServer)
    {
        _factory = new AuthWebApplicationFactory(sqlServer);
    }

    [Fact]
    public async Task Sixth_login_attempt_within_a_minute_returns_too_many_requests()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var client = CreateClient();
        var request = new LoginRequest("missing-user@test.local", "WrongPass123!");

        for (var attempt = 1; attempt <= 5; attempt++)
        {
            var response = await client.PostAsJsonAsync("/api/v1/auth/login", request, cancellationToken);
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }

        var sixthResponse = await client.PostAsJsonAsync("/api/v1/auth/login", request, cancellationToken);

        Assert.Equal(HttpStatusCode.TooManyRequests, sixthResponse.StatusCode);
        Assert.True(sixthResponse.Headers.Contains("Retry-After"));
    }

    [Fact]
    public async Task Thirty_first_availability_requests_within_a_minute_succeed_before_throttling()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var client = CreateClient();
        var from = DateTime.UtcNow;
        var to = from.AddDays(7);

        for (var attempt = 1; attempt <= 30; attempt++)
        {
            var response = await client.GetAsync(
                $"/api/v1/availability?from={Uri.EscapeDataString(from.ToString("O"))}&to={Uri.EscapeDataString(to.ToString("O"))}",
                cancellationToken);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        var thirtyFirstResponse = await client.GetAsync(
            $"/api/v1/availability?from={Uri.EscapeDataString(from.ToString("O"))}&to={Uri.EscapeDataString(to.ToString("O"))}",
            cancellationToken);

        Assert.Equal(HttpStatusCode.TooManyRequests, thirtyFirstResponse.StatusCode);
        Assert.True(thirtyFirstResponse.Headers.Contains("Retry-After"));
    }

    public void Dispose()
    {
        _factory.Dispose();
    }

    private HttpClient CreateClient()
    {
        return _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            HandleCookies = true,
            AllowAutoRedirect = false
        });
    }
}
