using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shora.Application.Common;
using Shora.Contracts.Auth;
using Shora.Contracts.Availability;
using Shora.Domain.Entities;
using Shora.Infrastructure.Data;
using Shora.Tests.Common;

namespace Shora.Tests.Integration.Api;

[Collection("SqlServer")]
public class AdminAvailabilityEndpointTests : IDisposable
{
    private readonly AuthWebApplicationFactory _factory;

    public AdminAvailabilityEndpointTests(SqlServerFixture sqlServer)
    {
        _factory = new AuthWebApplicationFactory(sqlServer);
    }

    [Fact]
    public async Task List_windows_as_admin_returns_seeded_windows()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var adminClient = await CreateAdminClientAsync(cancellationToken);

        var response = await adminClient.GetAsync("/api/v1/admin/availability-windows", cancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<List<AvailabilityWindowResponse>>(cancellationToken);
        Assert.NotNull(body);
        Assert.Equal(5, body!.Count);
        Assert.All(body, window => Assert.True(window.IsActive));
    }

    [Fact]
    public async Task List_windows_as_client_returns_forbidden()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var client = await CreateVerifiedClientAsync("admin-availability-client@example.com", cancellationToken);

        var response = await client.GetAsync("/api/v1/admin/availability-windows", cancellationToken);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Create_window_persists_and_regenerates_open_slots()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var adminClient = await CreateAdminClientAsync(cancellationToken);

        var beforeCount = await CountOpenSlotsAsync(cancellationToken);

        var createRequest = new CreateAvailabilityWindowRequest(
            DayOfWeek.Friday,
            new TimeSpan(10, 0, 0),
            new TimeSpan(13, 0, 0),
            IsActive: true);

        var response = await adminClient.PostAsJsonAsync(
            "/api/v1/admin/availability-windows",
            createRequest,
            cancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<AvailabilityWindowResponse>(cancellationToken);
        Assert.NotNull(body);
        Assert.Equal(DayOfWeek.Friday, body!.DayOfWeek);
        Assert.Equal(createRequest.StartTime, body.StartTime);
        Assert.Equal(createRequest.EndTime, body.EndTime);

        var listResponse = await adminClient.GetAsync("/api/v1/admin/availability-windows", cancellationToken);
        var windows = await listResponse.Content.ReadFromJsonAsync<List<AvailabilityWindowResponse>>(cancellationToken);
        Assert.Contains(windows!, window => window.Id == body.Id);

        var afterCount = await CountOpenSlotsAsync(cancellationToken);
        Assert.True(afterCount >= beforeCount);
    }

    [Fact]
    public async Task Update_window_persists_changes()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var adminClient = await CreateAdminClientAsync(cancellationToken);

        var existing = (await GetWindowsAsync(adminClient, cancellationToken)).First(window => window.DayOfWeek == DayOfWeek.Monday);
        var updateRequest = new UpdateAvailabilityWindowRequest(
            DayOfWeek.Monday,
            new TimeSpan(15, 0, 0),
            new TimeSpan(20, 0, 0),
            IsActive: true);

        var response = await adminClient.PutAsJsonAsync(
            $"/api/v1/admin/availability-windows/{existing.Id}",
            updateRequest,
            cancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<AvailabilityWindowResponse>(cancellationToken);
        Assert.NotNull(body);
        Assert.Equal(new TimeSpan(15, 0, 0), body!.StartTime);
        Assert.Equal(new TimeSpan(20, 0, 0), body.EndTime);
    }

    [Fact]
    public async Task Delete_window_removes_row()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var adminClient = await CreateAdminClientAsync(cancellationToken);

        var createResponse = await adminClient.PostAsJsonAsync(
            "/api/v1/admin/availability-windows",
            new CreateAvailabilityWindowRequest(
                DayOfWeek.Saturday,
                new TimeSpan(12, 0, 0),
                new TimeSpan(14, 0, 0)),
            cancellationToken);
        var created = await createResponse.Content.ReadFromJsonAsync<AvailabilityWindowResponse>(cancellationToken);
        Assert.NotNull(created);

        var deleteResponse = await adminClient.DeleteAsync(
            $"/api/v1/admin/availability-windows/{created!.Id}",
            cancellationToken);
        Assert.Equal(HttpStatusCode.OK, deleteResponse.StatusCode);

        var windows = await GetWindowsAsync(adminClient, cancellationToken);
        Assert.DoesNotContain(windows, window => window.Id == created.Id);
    }

    [Fact]
    public async Task Create_window_rejects_invalid_range_with_field_errors()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var adminClient = await CreateAdminClientAsync(cancellationToken);

        var response = await adminClient.PostAsJsonAsync(
            "/api/v1/admin/availability-windows",
            new CreateAvailabilityWindowRequest(
                DayOfWeek.Tuesday,
                new TimeSpan(18, 0, 0),
                new TimeSpan(16, 0, 0)),
            cancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>(cancellationToken);
        Assert.NotNull(problem);
        Assert.Equal(ErrorCodes.General.Validation, problem!.Extensions["code"]?.ToString());
        Assert.True(problem.Errors.ContainsKey("endTime"));
    }

    [Fact]
    public async Task Update_missing_window_returns_not_found()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var adminClient = await CreateAdminClientAsync(cancellationToken);

        var response = await adminClient.PutAsJsonAsync(
            $"/api/v1/admin/availability-windows/{Guid.NewGuid()}",
            new UpdateAvailabilityWindowRequest(
                DayOfWeek.Wednesday,
                new TimeSpan(16, 0, 0),
                new TimeSpan(21, 0, 0),
                IsActive: true),
            cancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        await AssertProblemCodeAsync(response, ErrorCodes.Availability.WindowNotFound, cancellationToken);
    }

    [Fact]
    public async Task Generate_after_save_does_not_remove_booked_slots()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var adminClient = await CreateAdminClientAsync(cancellationToken);

        Guid bookedSlotId;
        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var bookedSlot = await context.AvailabilitySlots.FirstAsync(cancellationToken);
            bookedSlot.IsBooked = true;
            bookedSlotId = bookedSlot.Id;
            await context.SaveChangesAsync(cancellationToken);
        }

        await adminClient.PostAsJsonAsync(
            "/api/v1/admin/availability-windows",
            new CreateAvailabilityWindowRequest(
                DayOfWeek.Friday,
                new TimeSpan(9, 0, 0),
                new TimeSpan(12, 0, 0)),
            cancellationToken);

        await using var verifyScope = _factory.Services.CreateAsyncScope();
        var verifyContext = verifyScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var persisted = await verifyContext.AvailabilitySlots.SingleAsync(slot => slot.Id == bookedSlotId, cancellationToken);
        Assert.True(persisted.IsBooked);
    }

    public void Dispose()
    {
        _factory.Dispose();
    }

    private async Task<List<AvailabilityWindowResponse>> GetWindowsAsync(
        HttpClient adminClient,
        CancellationToken cancellationToken)
    {
        var response = await adminClient.GetAsync("/api/v1/admin/availability-windows", cancellationToken);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<List<AvailabilityWindowResponse>>(cancellationToken))!;
    }

    private async Task<int> CountOpenSlotsAsync(CancellationToken cancellationToken)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        return await context.AvailabilitySlots.CountAsync(slot => !slot.IsBooked, cancellationToken);
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

    private static async Task AssertProblemCodeAsync(
        HttpResponseMessage response,
        string expectedCode,
        CancellationToken cancellationToken)
    {
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>(cancellationToken);
        Assert.NotNull(problem);
        Assert.Equal(expectedCode, problem!.Extensions["code"]?.ToString());
    }
}
