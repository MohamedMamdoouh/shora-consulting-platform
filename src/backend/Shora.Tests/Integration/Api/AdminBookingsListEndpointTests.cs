using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shora.Application.Bookings;
using Shora.Application.Common;
using Shora.Contracts.Auth;
using Shora.Contracts.Booking;
using Shora.Domain.Entities;
using Shora.Domain.Enums;
using Shora.Infrastructure.Data;
using Shora.Tests.Common;
using ContractCancellationRequestStatus = Shora.Contracts.Booking.CancellationRequestStatus;
using ContractDeliveryMethod = Shora.Contracts.Booking.DeliveryMethod;

namespace Shora.Tests.Integration.Api;

[Collection("SqlServer")]
public class AdminBookingsListEndpointTests : IDisposable
{
    private readonly AuthWebApplicationFactory _factory;

    public AdminBookingsListEndpointTests(SqlServerFixture sqlServer)
    {
        _factory = new AuthWebApplicationFactory(sqlServer);
    }

    [Fact]
    public async Task List_bookings_as_admin_returns_paginated_response()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var adminClient = await CreateAdminClientAsync(cancellationToken);
        var (_, userId) = await CreateVerifiedClientAsync("admin-list-bookings@example.com", cancellationToken);
        await ReserveBookingAsync(userId, "admin-list-bookings@example.com", cancellationToken);

        var response = await adminClient.GetAsync("/api/v1/admin/bookings?page=1&pageSize=20", cancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<AdminBookingsResponse>(cancellationToken);
        Assert.NotNull(body);
        Assert.True(body!.TotalCount >= 1);
        Assert.NotEmpty(body.Items);
        Assert.Equal(1, body.Page);
        Assert.Equal(20, body.PageSize);
        Assert.False(string.IsNullOrWhiteSpace(body.Items[0].ClientDisplayName));
    }

    [Fact]
    public async Task List_bookings_as_client_returns_forbidden()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var (client, _) = await CreateVerifiedClientAsync("admin-list-forbidden@example.com", cancellationToken);

        var response = await client.GetAsync("/api/v1/admin/bookings", cancellationToken);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task List_bookings_filters_by_status()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var adminClient = await CreateAdminClientAsync(cancellationToken);
        var (_, userId) = await CreateVerifiedClientAsync("admin-list-status@example.com", cancellationToken);
        var bookingId = await ReserveBookingAsync(userId, "admin-list-status@example.com", cancellationToken);

        var response = await adminClient.GetAsync(
            "/api/v1/admin/bookings?status=PendingPayment&page=1&pageSize=20",
            cancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<AdminBookingsResponse>(cancellationToken);
        Assert.NotNull(body);
        Assert.Contains(body!.Items, item => item.BookingId == bookingId);
        Assert.All(body.Items, item => Assert.Equal(nameof(BookingStatus.PendingPayment), item.Status));
    }

    [Fact]
    public async Task List_bookings_includes_voice_call_contact_phone_and_refund_due()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var adminClient = await CreateAdminClientAsync(cancellationToken);
        var (_, userId) = await CreateVerifiedClientAsync("admin-list-voice@example.com", cancellationToken);
        var bookingId = await SeedCancelledBookingAsync(
            userId,
            AuditActor.Client,
            PaymentStatus.Approved,
            cancellationToken);

        var response = await adminClient.GetAsync(
            "/api/v1/admin/bookings?status=Cancelled&page=1&pageSize=20",
            cancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<AdminBookingsResponse>(cancellationToken);
        Assert.NotNull(body);

        var item = Assert.Single(body!.Items, row => row.BookingId == bookingId);
        Assert.Equal(MyBookingLabelMapper.CancelledByYou, item.CancellationReasonLabel);
        Assert.True(item.RefundDue);
        Assert.Equal(nameof(PaymentStatus.Approved), item.PaymentStatus);
        Assert.Equal("+201012345678", item.ContactPhone);
        Assert.Equal(ContractDeliveryMethod.VoiceCall, item.DeliveryMethod);
    }

    [Fact]
    public async Task List_bookings_rejects_invalid_page()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var adminClient = await CreateAdminClientAsync(cancellationToken);

        var response = await adminClient.GetAsync("/api/v1/admin/bookings?page=0", cancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        await AssertProblemCodeAsync(response, ErrorCodes.General.Validation, cancellationToken);
    }

    [Fact]
    public async Task List_bookings_includes_pending_cancellation_request_metadata()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var adminClient = await CreateAdminClientAsync(cancellationToken);
        var (_, userId) = await CreateVerifiedClientAsync("admin-list-cancel-req@example.com", cancellationToken);
        var bookingId = await SeedCancellationRequestedBookingAsync(userId, cancellationToken);

        var response = await adminClient.GetAsync(
            "/api/v1/admin/bookings?status=CancellationRequested&page=1&pageSize=20",
            cancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<AdminBookingsResponse>(cancellationToken);
        Assert.NotNull(body);

        var item = Assert.Single(body!.Items, row => row.BookingId == bookingId);
        Assert.NotNull(item.CancellationRequest);
        Assert.Equal(ContractCancellationRequestStatus.Pending, item.CancellationRequest!.Status);
        Assert.Equal("Need to reschedule", item.CancellationRequest.ClientReason);
        Assert.True(item.CancellationRequest.AutoDeclineAtUtc > DateTime.UtcNow);
    }

    public void Dispose()
    {
        _factory.Dispose();
    }

    private async Task<Guid> ReserveBookingAsync(
        Guid userId,
        string email,
        CancellationToken cancellationToken)
    {
        var slotId = await GetOpenSlotIdAsync(cancellationToken);
        var (client, _) = await CreateVerifiedClientAsync(email, cancellationToken);

        var response = await client.PostAsJsonAsync("/api/v1/bookings", new CreateBookingRequest(
            slotId,
            ContractDeliveryMethod.Chat,
            null), cancellationToken);
        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadFromJsonAsync<ReserveBookingResponse>(cancellationToken);
        return body!.BookingId;
    }

    private async Task<Guid> SeedCancelledBookingAsync(
        Guid userId,
        AuditActor actor,
        PaymentStatus paymentStatus,
        CancellationToken cancellationToken)
    {
        var slotStartUtc = DateTime.UtcNow.AddDays(-2);
        var bookingId = Guid.NewGuid();
        var paymentId = Guid.NewGuid();
        var now = DateTime.UtcNow;

        await using var scope = _factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        context.Bookings.Add(new Booking
        {
            Id = bookingId,
            ClientId = userId,
            SlotStartUtc = slotStartUtc,
            SlotEndUtc = slotStartUtc.AddHours(1),
            DeliveryMethod = Domain.Enums.DeliveryMethod.VoiceCall,
            ContactPhone = "+201012345678",
            Status = BookingStatus.Cancelled,
            CreatedAt = now.AddDays(-3)
        });

        context.Payments.Add(new Payment
        {
            Id = paymentId,
            BookingId = bookingId,
            Status = paymentStatus,
            Amount = 500m,
            Currency = "EGP",
            CreatedAt = now.AddDays(-3),
            UpdatedAt = now.AddDays(-1)
        });

        context.BookingStatusAudits.Add(new BookingStatusAudit
        {
            Id = Guid.NewGuid(),
            BookingId = bookingId,
            FromStatus = BookingStatus.Confirmed,
            ToStatus = BookingStatus.Cancelled,
            Actor = actor,
            Reason = "Test cancellation",
            AtUtc = now.AddDays(-1)
        });

        await context.SaveChangesAsync(cancellationToken);
        return bookingId;
    }

    private async Task<Guid> SeedCancellationRequestedBookingAsync(
        Guid userId,
        CancellationToken cancellationToken)
    {
        var slotStartUtc = DateTime.UtcNow.AddDays(2);
        var bookingId = Guid.NewGuid();
        var paymentId = Guid.NewGuid();
        var now = DateTime.UtcNow;
        var autoDeclineAtUtc = slotStartUtc.AddHours(-1);

        await using var scope = _factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        context.Bookings.Add(new Booking
        {
            Id = bookingId,
            ClientId = userId,
            SlotStartUtc = slotStartUtc,
            SlotEndUtc = slotStartUtc.AddHours(1),
            DeliveryMethod = Domain.Enums.DeliveryMethod.Chat,
            Status = BookingStatus.CancellationRequested,
            CreatedAt = now
        });

        context.Payments.Add(new Payment
        {
            Id = paymentId,
            BookingId = bookingId,
            Status = PaymentStatus.Approved,
            Amount = 500m,
            Currency = "EGP",
            CreatedAt = now,
            UpdatedAt = now
        });

        context.CancellationRequests.Add(new CancellationRequest
        {
            Id = Guid.NewGuid(),
            BookingId = bookingId,
            RequestedByClientId = userId,
            RequestedAtUtc = now,
            ClientReason = "Need to reschedule",
            AutoDeclineAtUtc = autoDeclineAtUtc,
            Status = Domain.Enums.CancellationRequestStatus.Pending,
            ReopenCount = 0
        });

        await context.SaveChangesAsync(cancellationToken);
        return bookingId;
    }

    private async Task<Guid> GetOpenSlotIdAsync(CancellationToken cancellationToken)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        return (await context.AvailabilitySlots.AsNoTracking().FirstAsync(slot => !slot.IsBooked, cancellationToken)).Id;
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

    private async Task<(HttpClient Client, Guid UserId)> CreateVerifiedClientAsync(
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

        return (client, user!.Id);
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
