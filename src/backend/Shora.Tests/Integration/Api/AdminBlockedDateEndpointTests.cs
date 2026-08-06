using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shora.Application.Common;
using Shora.Contracts.Auth;
using Shora.Contracts.Availability;
using Shora.Contracts.Booking;
using Shora.Domain.Entities;
using Shora.Domain.Enums;
using Shora.Infrastructure.Data;
using Shora.Tests.Common;
using ContractDeliveryMethod = Shora.Contracts.Booking.DeliveryMethod;

namespace Shora.Tests.Integration.Api;

[Collection("SqlServer")]
public class AdminBlockedDateEndpointTests : IDisposable
{
    private readonly AuthWebApplicationFactory _factory;

    public AdminBlockedDateEndpointTests(SqlServerFixture sqlServer)
    {
        _factory = new AuthWebApplicationFactory(sqlServer);
    }

    [Fact]
    public async Task List_blocked_dates_as_admin_returns_empty_initially()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var adminClient = await CreateAdminClientAsync(cancellationToken);

        var response = await adminClient.GetAsync("/api/v1/admin/blocked-dates", cancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<List<BlockedDateResponse>>(cancellationToken);
        Assert.NotNull(body);
        Assert.Empty(body!);
    }

    [Fact]
    public async Task List_blocked_dates_as_client_returns_forbidden()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var client = await CreateVerifiedClientAsync("blocked-dates-client@example.com", cancellationToken);

        var response = await client.GetAsync("/api/v1/admin/blocked-dates", cancellationToken);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Create_blocked_date_removes_overlapping_open_slots()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var adminClient = await CreateAdminClientAsync(cancellationToken);

        var slot = await GetOpenSlotAsync(cancellationToken);
        var request = new CreateBlockedDateRequest(
            slot.StartTimeUtc,
            slot.EndTimeUtc,
            "Vacation");

        var response = await adminClient.PostAsJsonAsync("/api/v1/admin/blocked-dates", request, cancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<BlockedDateResponse>(cancellationToken);
        Assert.NotNull(body);
        Assert.Equal(request.StartUtc, body!.StartUtc);
        Assert.Equal(request.EndUtc, body.EndUtc);
        Assert.Equal(request.Reason, body.Reason);

        await using var scope = _factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        Assert.False(await context.AvailabilitySlots.AnyAsync(s => s.Id == slot.Id, cancellationToken));
        Assert.True(await context.BlockedDates.AnyAsync(b => b.Id == body.Id, cancellationToken));
    }

    [Fact]
    public async Task Create_blocked_date_returns_conflict_when_active_booking_overlaps()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var adminClient = await CreateAdminClientAsync(cancellationToken);
        var (_, bookingId, slotId) = await ReserveBookingAsync("blocked-date-conflict@example.com", cancellationToken);

        await using var scope = _factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var slot = await context.AvailabilitySlots.AsNoTracking().SingleAsync(s => s.Id == slotId, cancellationToken);

        var request = new CreateBlockedDateRequest(slot.StartTimeUtc, slot.EndTimeUtc, "Should fail");

        var response = await adminClient.PostAsJsonAsync("/api/v1/admin/blocked-dates", request, cancellationToken);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);

        var problemJson = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
        Assert.Equal(ErrorCodes.Availability.BlockedRangeConflictsWithBookings, problemJson.GetProperty("code").GetString());
        var conflictingIds = problemJson.GetProperty("conflictingBookingIds").EnumerateArray().Select(item => item.GetGuid()).ToList();
        Assert.Contains(bookingId, conflictingIds);

        Assert.True(await context.AvailabilitySlots.AnyAsync(s => s.Id == slotId, cancellationToken));
        Assert.False(await context.BlockedDates.AnyAsync(b => b.StartUtc == slot.StartTimeUtc, cancellationToken));
    }

    [Fact]
    public async Task Create_blocked_date_rejects_invalid_range()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var adminClient = await CreateAdminClientAsync(cancellationToken);
        var startUtc = DateTime.UtcNow.AddDays(5);

        var response = await adminClient.PostAsJsonAsync(
            "/api/v1/admin/blocked-dates",
            new CreateBlockedDateRequest(startUtc, startUtc, null),
            cancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        await AssertProblemCodeAsync(response, ErrorCodes.General.Validation, cancellationToken);
    }

    [Fact]
    public async Task Delete_blocked_date_removes_row()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var adminClient = await CreateAdminClientAsync(cancellationToken);
        var slot = await GetOpenSlotAsync(cancellationToken);

        var createResponse = await adminClient.PostAsJsonAsync(
            "/api/v1/admin/blocked-dates",
            new CreateBlockedDateRequest(slot.StartTimeUtc, slot.EndTimeUtc, "Temp"),
            cancellationToken);
        createResponse.EnsureSuccessStatusCode();

        var created = await createResponse.Content.ReadFromJsonAsync<BlockedDateResponse>(cancellationToken);
        Assert.NotNull(created);

        var deleteResponse = await adminClient.DeleteAsync($"/api/v1/admin/blocked-dates/{created!.Id}", cancellationToken);

        Assert.Equal(HttpStatusCode.OK, deleteResponse.StatusCode);

        await using var scope = _factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.False(await context.BlockedDates.AnyAsync(b => b.Id == created.Id, cancellationToken));
    }

    [Fact]
    public async Task Delete_missing_blocked_date_returns_not_found()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var adminClient = await CreateAdminClientAsync(cancellationToken);

        var response = await adminClient.DeleteAsync($"/api/v1/admin/blocked-dates/{Guid.NewGuid()}", cancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        await AssertProblemCodeAsync(response, ErrorCodes.Availability.BlockedDateNotFound, cancellationToken);
    }

    public void Dispose()
    {
        _factory.Dispose();
    }

    private async Task<AvailabilitySlot> GetOpenSlotAsync(CancellationToken cancellationToken)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        return await context.AvailabilitySlots.AsNoTracking().FirstAsync(slot => !slot.IsBooked, cancellationToken);
    }

    private async Task<Guid> GetOpenSlotIdAsync(CancellationToken cancellationToken)
    {
        var slot = await GetOpenSlotAsync(cancellationToken);
        return slot.Id;
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

    private async Task<(HttpClient Client, Guid BookingId, Guid SlotId)> ReserveBookingAsync(
        string email,
        CancellationToken cancellationToken)
    {
        var slotId = await GetOpenSlotIdAsync(cancellationToken);
        var client = await CreateVerifiedClientAsync(email, cancellationToken);

        var response = await client.PostAsJsonAsync("/api/v1/bookings", new CreateBookingRequest(
            slotId,
            ContractDeliveryMethod.Chat,
            null), cancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<ReserveBookingResponse>(cancellationToken);
        return (client, body!.BookingId, slotId);
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
