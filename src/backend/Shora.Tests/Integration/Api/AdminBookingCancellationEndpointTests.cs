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
using Shora.Contracts.Booking;
using Shora.Domain.Entities;
using Shora.Domain.Enums;
using Shora.Infrastructure.Data;
using Shora.Tests.Common;
using ContractCancellationDecisionReasonCode = Shora.Contracts.Booking.CancellationDecisionReasonCode;
using ContractDeliveryMethod = Shora.Contracts.Booking.DeliveryMethod;

namespace Shora.Tests.Integration.Api;

[Collection("SqlServer")]
public class AdminBookingCancellationEndpointTests : IDisposable
{
    private readonly AuthWebApplicationFactory _factory;

    public AdminBookingCancellationEndpointTests(SqlServerFixture sqlServer)
    {
        _factory = new AuthWebApplicationFactory(sqlServer);
    }

    [Fact]
    public async Task Cancel_unpaid_hold_voids_payment_and_releases_slot()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var (_, bookingId, slotId) = await ReserveBookingAsync("admin-cancel-hold@example.com", cancellationToken);

        var adminClient = await CreateAdminClientAsync(cancellationToken);
        var response = await adminClient.PostAsync(
            $"/api/v1/admin/bookings/{bookingId}/cancel",
            null,
            cancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<AdminBookingCancellationResponse>(cancellationToken);
        Assert.NotNull(body);
        Assert.Equal(bookingId, body!.BookingId);
        Assert.Equal(nameof(BookingStatus.Cancelled), body.BookingStatus);
        Assert.Equal(nameof(PaymentStatus.Void), body.PaymentStatus);
        Assert.False(body.RefundDue);

        await using var scope = _factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var booking = await context.Bookings.AsNoTracking().SingleAsync(b => b.Id == bookingId, cancellationToken);
        Assert.Equal(BookingStatus.Cancelled, booking.Status);
        Assert.Null(booking.AvailabilitySlotId);

        var slot = await context.AvailabilitySlots.AsNoTracking().SingleAsync(s => s.Id == slotId, cancellationToken);
        Assert.False(slot.IsBooked);
        Assert.Null(slot.BookingId);

        var payment = await context.Payments.AsNoTracking().SingleAsync(p => p.BookingId == bookingId, cancellationToken);
        Assert.Equal(PaymentStatus.Void, payment.Status);

        var outbox = await context.OutboxMessages.AsNoTracking().SingleAsync(
            m => m.AggregateId == bookingId && m.MessageType == OutboxMessageTypes.ClientBookingCancelledEmail,
            cancellationToken);
        Assert.Equal(OutboxMessageStatus.Pending, outbox.Status);
    }

    [Fact]
    public async Task Cancel_confirmed_paid_booking_keeps_payment_approved_for_refund_due()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var (_, bookingId, slotId) = await CreateConfirmedBookingAsync("admin-cancel-paid@example.com", cancellationToken);

        var adminClient = await CreateAdminClientAsync(cancellationToken);
        var response = await adminClient.PostAsync(
            $"/api/v1/admin/bookings/{bookingId}/cancel",
            null,
            cancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<AdminBookingCancellationResponse>(cancellationToken);
        Assert.NotNull(body);
        Assert.True(body!.RefundDue);
        Assert.Equal(nameof(PaymentStatus.Approved), body.PaymentStatus);

        await using var scope = _factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var payment = await context.Payments.AsNoTracking().SingleAsync(p => p.BookingId == bookingId, cancellationToken);
        Assert.Equal(PaymentStatus.Approved, payment.Status);

        var slot = await context.AvailabilitySlots.AsNoTracking().SingleAsync(s => s.Id == slotId, cancellationToken);
        Assert.False(slot.IsBooked);
    }

    [Fact]
    public async Task Cancel_rejects_after_session_start()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var (_, bookingId, _) = await CreateConfirmedBookingAsync("admin-cancel-too-late@example.com", cancellationToken);
        await SetBookingSlotStartAsync(bookingId, DateTime.UtcNow.AddMinutes(-5), cancellationToken);

        var adminClient = await CreateAdminClientAsync(cancellationToken);
        var response = await adminClient.PostAsync(
            $"/api/v1/admin/bookings/{bookingId}/cancel",
            null,
            cancellationToken);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        await AssertProblemCodeAsync(response, ErrorCodes.Booking.InvalidStatus, cancellationToken);
    }

    [Fact]
    public async Task Approve_cancellation_request_cancels_booking_and_keeps_refund_due()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var (client, bookingId, slotId) = await CreateConfirmedBookingAsync("admin-approve-cancel@example.com", cancellationToken);

        var requestResponse = await client.PostAsJsonAsync(
            $"/api/v1/bookings/{bookingId}/cancellation-requests",
            new CancellationRequestBody("Need to reschedule"),
            cancellationToken);
        Assert.Equal(HttpStatusCode.OK, requestResponse.StatusCode);

        var adminClient = await CreateAdminClientAsync(cancellationToken);
        var response = await adminClient.PostAsync(
            $"/api/v1/admin/bookings/{bookingId}/cancellation-requests/approve",
            null,
            cancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<AdminBookingCancellationResponse>(cancellationToken);
        Assert.NotNull(body);
        Assert.Equal(nameof(BookingStatus.Cancelled), body!.BookingStatus);
        Assert.True(body.RefundDue);

        await using var scope = _factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var booking = await context.Bookings.AsNoTracking().SingleAsync(b => b.Id == bookingId, cancellationToken);
        Assert.Equal(BookingStatus.Cancelled, booking.Status);
        Assert.Null(booking.AvailabilitySlotId);

        var request = await context.CancellationRequests.AsNoTracking().SingleAsync(r => r.BookingId == bookingId, cancellationToken);
        Assert.Equal(Domain.Enums.CancellationRequestStatus.Approved, request.Status);
        Assert.NotNull(request.ReviewedAtUtc);

        var slot = await context.AvailabilitySlots.AsNoTracking().SingleAsync(s => s.Id == slotId, cancellationToken);
        Assert.False(slot.IsBooked);
    }

    [Fact]
    public async Task Approve_cancellation_request_rejects_after_session_start()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var (client, bookingId, slotId) = await CreateConfirmedBookingAsync("admin-approve-cancel-too-late@example.com", cancellationToken);

        var requestResponse = await client.PostAsJsonAsync(
            $"/api/v1/bookings/{bookingId}/cancellation-requests",
            new CancellationRequestBody("Need to reschedule"),
            cancellationToken);
        Assert.Equal(HttpStatusCode.OK, requestResponse.StatusCode);

        await SetBookingSlotStartAsync(bookingId, DateTime.UtcNow.AddMinutes(-5), cancellationToken);

        var adminClient = await CreateAdminClientAsync(cancellationToken);
        var response = await adminClient.PostAsync(
            $"/api/v1/admin/bookings/{bookingId}/cancellation-requests/approve",
            null,
            cancellationToken);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        await AssertProblemCodeAsync(response, ErrorCodes.Booking.InvalidStatus, cancellationToken);

        await using var scope = _factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var booking = await context.Bookings.AsNoTracking().SingleAsync(b => b.Id == bookingId, cancellationToken);
        Assert.Equal(BookingStatus.CancellationRequested, booking.Status);
        Assert.Equal(slotId, booking.AvailabilitySlotId);

        var request = await context.CancellationRequests.AsNoTracking().SingleAsync(r => r.BookingId == bookingId, cancellationToken);
        Assert.Equal(Domain.Enums.CancellationRequestStatus.Pending, request.Status);
        Assert.Null(request.ReviewedAtUtc);

        var slot = await context.AvailabilitySlots.AsNoTracking().SingleAsync(s => s.Id == slotId, cancellationToken);
        Assert.True(slot.IsBooked);
        Assert.Equal(bookingId, slot.BookingId);
    }

    [Fact]
    public async Task Decline_cancellation_request_returns_booking_to_confirmed_and_enqueues_email()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var (client, bookingId, _) = await CreateConfirmedBookingAsync("admin-decline-cancel@example.com", cancellationToken);

        var requestResponse = await client.PostAsJsonAsync(
            $"/api/v1/bookings/{bookingId}/cancellation-requests",
            new CancellationRequestBody("Changed my mind"),
            cancellationToken);
        Assert.Equal(HttpStatusCode.OK, requestResponse.StatusCode);

        var adminClient = await CreateAdminClientAsync(cancellationToken);
        var response = await adminClient.PostAsJsonAsync(
            $"/api/v1/admin/bookings/{bookingId}/cancellation-requests/decline",
            new DeclineCancellationRequestBody(ContractCancellationDecisionReasonCode.Policy, "Session stands"),
            cancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<AdminBookingCancellationResponse>(cancellationToken);
        Assert.NotNull(body);
        Assert.Equal(nameof(BookingStatus.Confirmed), body!.BookingStatus);
        Assert.False(body.RefundDue);

        await using var scope = _factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var booking = await context.Bookings.AsNoTracking().SingleAsync(b => b.Id == bookingId, cancellationToken);
        Assert.Equal(BookingStatus.Confirmed, booking.Status);

        var request = await context.CancellationRequests.AsNoTracking().SingleAsync(r => r.BookingId == bookingId, cancellationToken);
        Assert.Equal(Domain.Enums.CancellationRequestStatus.Declined, request.Status);
        Assert.Equal(DecisionReasonCode.Policy, request.DecisionReasonCode);
        Assert.Equal("Session stands", request.DecisionReason);
        Assert.Null(request.ClientDecisionSeenAtUtc);

        var outbox = await context.OutboxMessages.AsNoTracking().SingleAsync(
            m => m.AggregateId == bookingId && m.MessageType == OutboxMessageTypes.ClientCancellationRequestDeclinedEmail,
            cancellationToken);
        Assert.Equal(OutboxMessageStatus.Pending, outbox.Status);
    }

    [Fact]
    public async Task Cancel_rejects_non_admin()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var (client, bookingId, _) = await ReserveBookingAsync("admin-cancel-forbidden@example.com", cancellationToken);

        var response = await client.PostAsync(
            $"/api/v1/admin/bookings/{bookingId}/cancel",
            null,
            cancellationToken);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Decline_cancellation_request_rejects_invalid_reason_code()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var (client, bookingId, _) = await CreateConfirmedBookingAsync("admin-decline-invalid@example.com", cancellationToken);

        var requestResponse = await client.PostAsJsonAsync(
            $"/api/v1/bookings/{bookingId}/cancellation-requests",
            new CancellationRequestBody(null),
            cancellationToken);
        Assert.Equal(HttpStatusCode.OK, requestResponse.StatusCode);

        var adminClient = await CreateAdminClientAsync(cancellationToken);
        var response = await adminClient.PostAsJsonAsync(
            $"/api/v1/admin/bookings/{bookingId}/cancellation-requests/decline",
            new { reasonCode = 99, reasonNote = (string?)null },
            cancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        await AssertProblemCodeAsync(response, ErrorCodes.Cancellation.InvalidDecisionReason, cancellationToken);
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

    private async Task<(HttpClient Client, Guid UserId)> CreateVerifiedClientAsync(
        string email,
        CancellationToken cancellationToken)
    {
        var client = CreateClient();

        await client.PostAsJsonAsync("/api/v1/auth/signup", new SignUpRequest(
            email,
            "Password123!",
            "Test Client"), cancellationToken);

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

        return (client, user!.Id);
    }

    private async Task<Guid> GetOpenSlotIdAsync(CancellationToken cancellationToken)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        return (await context.AvailabilitySlots.AsNoTracking().FirstAsync(slot => !slot.IsBooked, cancellationToken)).Id;
    }

    private async Task<(HttpClient Client, Guid BookingId, Guid SlotId)> ReserveBookingAsync(
        string email,
        CancellationToken cancellationToken)
    {
        var slotId = await GetOpenSlotIdAsync(cancellationToken);
        var (client, _) = await CreateVerifiedClientAsync(email, cancellationToken);

        var response = await client.PostAsJsonAsync("/api/v1/bookings", new CreateBookingRequest(
            slotId,
            ContractDeliveryMethod.Chat,
            null), cancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<ReserveBookingResponse>(cancellationToken);
        return (client, body!.BookingId, slotId);
    }

    private async Task<(HttpClient Client, Guid BookingId, Guid SlotId)> CreateConfirmedBookingAsync(
        string email,
        CancellationToken cancellationToken)
    {
        var (client, bookingId, slotId) = await ReserveBookingAsync(email, cancellationToken);
        await SetBookingStatusAsync(bookingId, BookingStatus.Confirmed, PaymentStatus.Approved, cancellationToken);
        return (client, bookingId, slotId);
    }

    private async Task SetBookingStatusAsync(
        Guid bookingId,
        BookingStatus bookingStatus,
        PaymentStatus paymentStatus,
        CancellationToken cancellationToken)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var booking = await context.Bookings.SingleAsync(b => b.Id == bookingId, cancellationToken);
        booking.Status = bookingStatus;

        var payment = await context.Payments.SingleAsync(p => p.BookingId == bookingId, cancellationToken);
        payment.Status = paymentStatus;

        await context.SaveChangesAsync(cancellationToken);
    }

    private async Task SetBookingSlotStartAsync(
        Guid bookingId,
        DateTime slotStartUtc,
        CancellationToken cancellationToken)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var booking = await context.Bookings.SingleAsync(b => b.Id == bookingId, cancellationToken);
        booking.SlotStartUtc = slotStartUtc;
        booking.SlotEndUtc = slotStartUtc.AddHours(1);
        await context.SaveChangesAsync(cancellationToken);
    }

    private static async Task AssertProblemCodeAsync(
        HttpResponseMessage response,
        string expectedCode,
        CancellationToken cancellationToken)
    {
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>(cancellationToken);
        Assert.NotNull(problem);
        Assert.Equal(expectedCode, problem!.Extensions?["code"]?.ToString());
    }
}
