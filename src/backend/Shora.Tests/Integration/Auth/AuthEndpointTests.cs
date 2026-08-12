using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shora.Contracts.Auth;
using Shora.Contracts.Common;
using Shora.Api.Infrastructure;
using Shora.Application.Common;
using Shora.Domain.Entities;
using Shora.Infrastructure.Data;
using Shora.Infrastructure.Services;
using Shora.Tests.Common;

namespace Shora.Tests.Integration.Auth;

[Collection("Postgres")]
public class AuthEndpointTests : IDisposable
{
    private readonly AuthWebApplicationFactory _factory;

    public AuthEndpointTests(PostgresFixture sqlServer)
    {
        _factory = new AuthWebApplicationFactory(sqlServer);
    }

    public void Dispose()
    {
        _factory.Dispose();
    }

    [Fact]
    public async Task Signup_creates_client_and_returns_token()
    {
        var client = CreateClient();

        var response = await client.PostAsJsonAsync("/api/v1/auth/signup", new SignUpRequest(
            "client@example.com",
            "Password123!",
            "Test Client"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<AuthResponse>();
        Assert.NotNull(body);
        Assert.Equal("Client", body!.Role);
        Assert.False(body.EmailConfirmed);
        Assert.NotEmpty(body.AccessToken);
    }

    [Fact]
    public async Task Signup_duplicate_email_returns_conflict()
    {
        var client = CreateClient();

        await client.PostAsJsonAsync("/api/v1/auth/signup", new SignUpRequest(
            "dup@example.com",
            "Password123!",
            null));

        var response = await client.PostAsJsonAsync("/api/v1/auth/signup", new SignUpRequest(
            "dup@example.com",
            "Password123!",
            null));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);

        var problemJson = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(ApiProblemDetailsMapper.ErrorTypeFor(ErrorCodes.Auth.DuplicateEmail), problemJson.GetProperty("type").GetString());
        Assert.Equal(ErrorCodes.Auth.DuplicateEmail, problemJson.GetProperty("code").GetString());
        Assert.Equal(409, problemJson.GetProperty("status").GetInt32());
    }

    [Fact]
    public async Task Admin_login_returns_admin_role()
    {
        var client = CreateClient();

        var response = await client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest(
            "admin@test.local",
            "TestPass123!"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<AuthResponse>();
        Assert.Equal("Admin", body!.Role);
    }

    [Fact]
    public async Task Login_sets_refresh_cookie_and_refresh_returns_new_access_token()
    {
        var client = CreateClient();

        var loginResponse = await client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest(
            "admin@test.local",
            "TestPass123!"));

        Assert.True(loginResponse.Headers.TryGetValues("Set-Cookie", out var cookies));
        Assert.Contains("shora_refresh=", string.Join(';', cookies!));

        var loginBody = await loginResponse.Content.ReadFromJsonAsync<AuthResponse>();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", loginBody!.AccessToken);

        var refreshResponse = await client.PostAsync("/api/v1/auth/refresh", null);
        Assert.Equal(HttpStatusCode.OK, refreshResponse.StatusCode);

        var refreshBody = await refreshResponse.Content.ReadFromJsonAsync<AuthResponse>();
        Assert.NotEqual(loginBody.AccessToken, refreshBody!.AccessToken);
    }

    [Fact]
    public async Task Logout_clears_session()
    {
        var client = CreateClient();

        await client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest(
            "admin@test.local",
            "TestPass123!"));

        var logoutResponse = await client.PostAsync("/api/v1/auth/logout", null);
        Assert.Equal(HttpStatusCode.OK, logoutResponse.StatusCode);

        var refreshResponse = await client.PostAsync("/api/v1/auth/refresh", null);
        Assert.Equal(HttpStatusCode.Unauthorized, refreshResponse.StatusCode);
    }

    [Fact]
    public async Task Verify_email_sets_email_confirmed()
    {
        var client = CreateClient();

        await client.PostAsJsonAsync("/api/v1/auth/signup", new SignUpRequest(
            "verify@example.com",
            "Password123!",
            null));

        using var scope = _factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = await userManager.FindByEmailAsync("verify@example.com");
        Assert.NotNull(user);

        var token = await userManager.GenerateEmailConfirmationTokenAsync(user!);
        var verifyResponse = await client.PostAsJsonAsync("/api/v1/auth/verify-email", new VerifyEmailRequest(
            "verify@example.com",
            token));

        Assert.Equal(HttpStatusCode.OK, verifyResponse.StatusCode);

        using var verifyScope = _factory.Services.CreateScope();
        var refreshedUserManager = verifyScope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        user = await refreshedUserManager.FindByEmailAsync("verify@example.com");
        Assert.True(user!.EmailConfirmed);
    }

    [Fact]
    public async Task Verify_email_already_confirmed_returns_ok()
    {
        var client = CreateClient();

        await client.PostAsJsonAsync("/api/v1/auth/signup", new SignUpRequest(
            "already-verified@example.com",
            "Password123!",
            null));

        using var scope = _factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = await userManager.FindByEmailAsync("already-verified@example.com");
        Assert.NotNull(user);

        var token = await userManager.GenerateEmailConfirmationTokenAsync(user!);
        var firstVerify = await client.PostAsJsonAsync("/api/v1/auth/verify-email", new VerifyEmailRequest(
            "already-verified@example.com",
            token));
        Assert.Equal(HttpStatusCode.OK, firstVerify.StatusCode);

        var secondVerify = await client.PostAsJsonAsync("/api/v1/auth/verify-email", new VerifyEmailRequest(
            "already-verified@example.com",
            token));
        Assert.Equal(HttpStatusCode.OK, secondVerify.StatusCode);

        var body = await secondVerify.Content.ReadFromJsonAsync<MessageResponse>();
        Assert.Equal("Email already verified.", body!.Message);
    }

    [Fact]
    public async Task Reset_password_allows_login_with_new_password()
    {
        var client = CreateClient();

        await client.PostAsJsonAsync("/api/v1/auth/signup", new SignUpRequest(
            "reset@example.com",
            "Password123!",
            null));

        using var scope = _factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = await userManager.FindByEmailAsync("reset@example.com");
        var resetToken = await userManager.GeneratePasswordResetTokenAsync(user!);

        var resetResponse = await client.PostAsJsonAsync("/api/v1/auth/reset-password", new ResetPasswordRequest(
            "reset@example.com",
            resetToken,
            "NewPassword123!"));

        Assert.Equal(HttpStatusCode.OK, resetResponse.StatusCode);

        var refreshAfterReset = await client.PostAsync("/api/v1/auth/refresh", null);
        Assert.Equal(HttpStatusCode.Unauthorized, refreshAfterReset.StatusCode);

        var oldLogin = await client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest(
            "reset@example.com",
            "Password123!"));
        Assert.Equal(HttpStatusCode.Unauthorized, oldLogin.StatusCode);

        var newLogin = await client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest(
            "reset@example.com",
            "NewPassword123!"));
        Assert.Equal(HttpStatusCode.OK, newLogin.StatusCode);
    }

    [Fact]
    public async Task Google_sign_in_creates_confirmed_client()
    {
        var client = CreateClient();

        var response = await client.PostAsJsonAsync("/api/v1/auth/google", new GoogleSignInRequest("google-user-1"));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var scope = _factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = await userManager.FindByEmailAsync("google-user-1@google.test");
        Assert.NotNull(user);
        Assert.True(user!.EmailConfirmed);
    }

    [Fact]
    public async Task Google_pre_takeover_revokes_password_and_confirms_email()
    {
        var client = CreateClient();

        await client.PostAsJsonAsync("/api/v1/auth/signup", new SignUpRequest(
            "squatter@example.com",
            "Password123!",
            null));

        using (var scope = _factory.Services.CreateScope())
        {
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var user = await userManager.FindByEmailAsync("squatter@example.com");
            Assert.NotNull(user);
            Assert.False(user!.EmailConfirmed);
            Assert.True(await userManager.HasPasswordAsync(user));
        }

        var googleResponse = await client.PostAsJsonAsync("/api/v1/auth/google", new GoogleSignInRequest("email:squatter@example.com"));
        Assert.Equal(HttpStatusCode.OK, googleResponse.StatusCode);

        using (var scope = _factory.Services.CreateScope())
        {
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var user = await userManager.FindByEmailAsync("squatter@example.com");
            Assert.NotNull(user);
            Assert.True(user!.EmailConfirmed);
            Assert.False(await userManager.HasPasswordAsync(user));
        }

        var oldPasswordLogin = await client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest(
            "squatter@example.com",
            "Password123!"));
        Assert.Equal(HttpStatusCode.Unauthorized, oldPasswordLogin.StatusCode);
    }

    [Fact]
    public async Task Refresh_grace_window_allows_second_presented_token()
    {
        var client = CreateClient();

        var loginResponse = await client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest(
            "admin@test.local",
            "TestPass123!"));

        var originalRefreshToken = ExtractRefreshToken(loginResponse);
        Assert.NotNull(originalRefreshToken);

        var refreshClient1 = CreateClient();
        refreshClient1.DefaultRequestHeaders.Add("Cookie", $"shora_refresh={originalRefreshToken}");
        var firstRefresh = await refreshClient1.PostAsync("/api/v1/auth/refresh", null);
        Assert.Equal(HttpStatusCode.OK, firstRefresh.StatusCode);

        var refreshClient2 = CreateClient();
        refreshClient2.DefaultRequestHeaders.Add("Cookie", $"shora_refresh={originalRefreshToken}");
        var secondRefresh = await refreshClient2.PostAsync("/api/v1/auth/refresh", null);
        Assert.Equal(HttpStatusCode.OK, secondRefresh.StatusCode);
    }

    [Fact]
    public async Task Refresh_reuse_after_grace_revokes_all_tokens()
    {
        var client = CreateClient();

        var loginResponse = await client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest(
            "admin@test.local",
            "TestPass123!"));

        var originalRefreshToken = ExtractRefreshToken(loginResponse);
        Assert.NotNull(originalRefreshToken);

        var firstRefresh = await client.PostAsync("/api/v1/auth/refresh", null);
        Assert.Equal(HttpStatusCode.OK, firstRefresh.StatusCode);

        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var originalHash = RefreshCookieService.HashToken(originalRefreshToken);
        var revoked = await context.RefreshTokens.FirstAsync(t => t.TokenHash == originalHash);
        revoked.RevokedAtUtc = DateTime.UtcNow.AddMinutes(-2);
        await context.SaveChangesAsync();

        var reuseClient = CreateClient();
        reuseClient.DefaultRequestHeaders.Add("Cookie", $"shora_refresh={originalRefreshToken}");
        var reuseResponse = await reuseClient.PostAsync("/api/v1/auth/refresh", null);
        Assert.Equal(HttpStatusCode.Unauthorized, reuseResponse.StatusCode);

        var followUp = await client.PostAsync("/api/v1/auth/refresh", null);
        Assert.Equal(HttpStatusCode.Unauthorized, followUp.StatusCode);
    }

    private HttpClient CreateClient()
    {
        return _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            HandleCookies = true,
            AllowAutoRedirect = false
        });
    }

    private static string? ExtractRefreshToken(HttpResponseMessage response)
    {
        if (!response.Headers.TryGetValues("Set-Cookie", out var values))
        {
            return null;
        }

        foreach (var cookie in values)
        {
            const string prefix = "shora_refresh=";
            var segment = cookie.Split(';')[0];
            if (segment.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                return segment[prefix.Length..];
            }
        }

        return null;
    }
}
