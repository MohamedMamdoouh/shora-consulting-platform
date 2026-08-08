using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Reflection;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Shora.Application.Ops;
using Shora.Contracts.Auth;
using Shora.Contracts.Ops;
using Shora.Domain.Entities;
using Shora.Tests.Common;

namespace Shora.Tests.Integration.Api;

[Collection("SqlServer")]
public class AdminOpsEndpointTests : IDisposable
{
    private readonly AuthWebApplicationFactory _factory;

    public AdminOpsEndpointTests(SqlServerFixture sqlServer)
    {
        _factory = new AuthWebApplicationFactory(sqlServer);
    }

    [Fact]
    public async Task Get_alerts_as_admin_returns_response_shape()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var adminClient = await CreateAdminClientAsync(cancellationToken);

        var response = await adminClient.GetAsync("/api/v1/admin/ops/alerts", cancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<AdminOpsAlertsResponse>(cancellationToken);
        Assert.NotNull(body);
        Assert.NotNull(body!.Alerts);
    }

    [Fact]
    public async Task Get_alerts_as_client_returns_forbidden()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var client = await CreateVerifiedClientAsync("admin-ops-alerts-client@example.com", cancellationToken);

        var response = await client.GetAsync("/api/v1/admin/ops/alerts", cancellationToken);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Get_alerts_unauthenticated_returns_unauthorized()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var client = CreateClient();

        var response = await client.GetAsync("/api/v1/admin/ops/alerts", cancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Get_runbooks_as_admin_returns_all_catalog_entries()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var adminClient = await CreateAdminClientAsync(cancellationToken);

        var response = await adminClient.GetAsync("/api/v1/admin/ops/runbooks", cancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<AdminOpsRunbooksResponse>(cancellationToken);
        Assert.NotNull(body);
        Assert.Equal(7, body!.Runbooks.Count);

        var expectedIds = typeof(OpsRunbookIds)
            .GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy)
            .Where(field => field is { IsLiteral: true, IsInitOnly: false } && field.FieldType == typeof(string))
            .Select(field => (string)field.GetRawConstantValue()!)
            .OrderBy(id => id)
            .ToList();

        var actualIds = body.Runbooks.Select(runbook => runbook.Id).OrderBy(id => id).ToList();
        Assert.Equal(expectedIds, actualIds);

        foreach (var runbook in body.Runbooks)
        {
            Assert.False(string.IsNullOrWhiteSpace(runbook.Owner));
            Assert.False(string.IsNullOrWhiteSpace(runbook.ResponseSla));
            Assert.False(string.IsNullOrWhiteSpace(runbook.Trigger));
            Assert.NotEmpty(runbook.Steps);
            Assert.All(runbook.Steps, step => Assert.False(string.IsNullOrWhiteSpace(step)));
        }
    }

    [Fact]
    public async Task Get_runbooks_as_client_returns_forbidden()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var client = await CreateVerifiedClientAsync("admin-ops-runbooks-client@example.com", cancellationToken);

        var response = await client.GetAsync("/api/v1/admin/ops/runbooks", cancellationToken);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Get_runbooks_unauthenticated_returns_unauthorized()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var client = CreateClient();

        var response = await client.GetAsync("/api/v1/admin/ops/runbooks", cancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
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

    private async Task<HttpClient> CreateAdminClientAsync(CancellationToken cancellationToken)
    {
        var client = CreateClient();
        var loginResponse = await client.PostAsJsonAsync(
            "/api/v1/auth/login",
            new LoginRequest("admin@test.local", "TestPass123!"),
            cancellationToken);
        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);

        var loginBody = await loginResponse.Content.ReadFromJsonAsync<AuthResponse>(cancellationToken);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", loginBody!.AccessToken);
        return client;
    }

    private async Task<HttpClient> CreateVerifiedClientAsync(
        string email,
        CancellationToken cancellationToken)
    {
        var client = CreateClient();

        await client.PostAsJsonAsync(
            "/api/v1/auth/signup",
            new SignUpRequest(email, "Password123!", "Test Client"),
            cancellationToken);

        await using var scope = _factory.Services.CreateAsyncScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = await userManager.FindByEmailAsync(email);
        Assert.NotNull(user);

        var token = await userManager.GenerateEmailConfirmationTokenAsync(user!);
        await client.PostAsJsonAsync("/api/v1/auth/verify-email", new VerifyEmailRequest(email, token), cancellationToken);

        var loginResponse = await client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest(
            email,
            "Password123!"), cancellationToken);
        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);
        var loginBody = await loginResponse.Content.ReadFromJsonAsync<AuthResponse>(cancellationToken);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", loginBody!.AccessToken);

        return client;
    }
}
