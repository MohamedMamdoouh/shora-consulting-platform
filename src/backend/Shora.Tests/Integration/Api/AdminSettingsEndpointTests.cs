using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Shora.Application.Common;
using Shora.Contracts.Auth;
using Shora.Contracts.Settings;
using Shora.Domain.Entities;
using Shora.Tests.Common;

namespace Shora.Tests.Integration.Api;

[Collection("SqlServer")]
public class AdminSettingsEndpointTests : IDisposable
{
    private readonly AuthWebApplicationFactory _factory;

    public AdminSettingsEndpointTests(SqlServerFixture sqlServer)
    {
        _factory = new AuthWebApplicationFactory(sqlServer);
    }

    [Fact]
    public async Task Get_settings_as_admin_returns_full_settings()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var adminClient = await CreateAdminClientAsync(cancellationToken);

        var response = await adminClient.GetAsync("/api/v1/admin/settings", cancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<AdminSettingsResponse>(cancellationToken);
        Assert.NotNull(body);
        Assert.Equal(500m, body!.SessionPrice);
        Assert.Equal(60, body.SessionDurationMinutes);
        Assert.Equal(15, body.BufferMinutes);
        Assert.Equal(60, body.ReceiptUploadWindowMinutes);
        Assert.Equal(1, body.CancellationRequestAutoDeclineHours);
        Assert.Equal("+201000000000", body.ConsultantWhatsAppNumber);
        Assert.Equal("01000000000", body.VodafoneCashNumber);
        Assert.Equal("consultant@instapay", body.InstaPayHandle);
        Assert.True(body.ReceiptRetentionMonths > 0);
    }

    [Fact]
    public async Task Get_settings_as_client_returns_forbidden()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var client = await CreateVerifiedClientAsync("admin-settings-client@example.com", cancellationToken);

        var response = await client.GetAsync("/api/v1/admin/settings", cancellationToken);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Get_settings_unauthenticated_returns_unauthorized()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var client = CreateClient();

        var response = await client.GetAsync("/api/v1/admin/settings", cancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Put_settings_persists_valid_update_and_refreshes_public_cache()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var adminClient = await CreateAdminClientAsync(cancellationToken);

        var update = new UpdateAdminSettingsRequest(
            SessionPrice: 550.50m,
            SessionDurationMinutes: 90,
            BufferMinutes: 10,
            ReceiptUploadWindowMinutes: 45,
            CancellationRequestAutoDeclineHours: 2,
            ConsultantWhatsAppNumber: "+201098765432",
            VodafoneCashNumber: "01098765432",
            InstaPayHandle: "updated@instapay",
            PaymentInstructions: "Updated payment note.");

        var putResponse = await adminClient.PutAsJsonAsync("/api/v1/admin/settings", update, cancellationToken);
        Assert.Equal(HttpStatusCode.OK, putResponse.StatusCode);

        var putBody = await putResponse.Content.ReadFromJsonAsync<AdminSettingsResponse>(cancellationToken);
        Assert.NotNull(putBody);
        Assert.Equal(update.SessionPrice, putBody!.SessionPrice);
        Assert.Equal(update.SessionDurationMinutes, putBody.SessionDurationMinutes);
        Assert.Equal(update.BufferMinutes, putBody.BufferMinutes);
        Assert.Equal("+201098765432", putBody.ConsultantWhatsAppNumber);
        Assert.Equal("01098765432", putBody.VodafoneCashNumber);
        Assert.Equal("updated@instapay", putBody.InstaPayHandle);
        Assert.Equal("Updated payment note.", putBody.PaymentInstructions);

        var getResponse = await adminClient.GetAsync("/api/v1/admin/settings", cancellationToken);
        var getBody = await getResponse.Content.ReadFromJsonAsync<AdminSettingsResponse>(cancellationToken);
        Assert.NotNull(getBody);
        Assert.Equal(update.SessionPrice, getBody!.SessionPrice);
        Assert.Equal(update.SessionDurationMinutes, getBody.SessionDurationMinutes);

        var publicClient = CreateClient();
        var publicResponse = await publicClient.GetAsync("/api/v1/settings/public", cancellationToken);
        var publicBody = await publicResponse.Content.ReadFromJsonAsync<PublicSettingsResponse>(cancellationToken);
        Assert.NotNull(publicBody);
        Assert.Equal(update.SessionPrice, publicBody!.SessionPrice);
        Assert.Equal(update.SessionDurationMinutes, publicBody.SessionDurationMinutes);
    }

    [Fact]
    public async Task Put_settings_rejects_invalid_session_price_with_field_errors()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var adminClient = await CreateAdminClientAsync(cancellationToken);

        var update = ValidUpdateRequest() with { SessionPrice = 0m };
        var response = await adminClient.PutAsJsonAsync("/api/v1/admin/settings", update, cancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>(cancellationToken);
        Assert.NotNull(problem);
        Assert.Equal(ErrorCodes.General.Validation, problem!.Extensions["code"]?.ToString());
        Assert.True(problem.Errors.ContainsKey("sessionPrice"));
    }

    [Fact]
    public async Task Put_settings_as_client_returns_forbidden()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var client = await CreateVerifiedClientAsync("admin-settings-put-client@example.com", cancellationToken);

        var response = await client.PutAsJsonAsync(
            "/api/v1/admin/settings",
            ValidUpdateRequest(),
            cancellationToken);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    public void Dispose()
    {
        _factory.Dispose();
    }

    private static UpdateAdminSettingsRequest ValidUpdateRequest() =>
        new(
            SessionPrice: 500m,
            SessionDurationMinutes: 60,
            BufferMinutes: 15,
            ReceiptUploadWindowMinutes: 60,
            CancellationRequestAutoDeclineHours: 1,
            ConsultantWhatsAppNumber: "+201012345678",
            VodafoneCashNumber: "01012345678",
            InstaPayHandle: "consultant@instapay",
            PaymentInstructions: null);

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
        await client.PostAsJsonAsync(
            "/api/v1/auth/verify-email",
            new VerifyEmailRequest(email, token),
            cancellationToken);

        var loginResponse = await client.PostAsJsonAsync(
            "/api/v1/auth/login",
            new LoginRequest(email, "Password123!"),
            cancellationToken);
        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);

        var loginBody = await loginResponse.Content.ReadFromJsonAsync<AuthResponse>(cancellationToken);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", loginBody!.AccessToken);

        return client;
    }
}
