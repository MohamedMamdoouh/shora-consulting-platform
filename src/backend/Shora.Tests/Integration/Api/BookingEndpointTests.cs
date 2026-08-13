using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shora.Application.Common;
using Shora.Contracts.Auth;
using Shora.Contracts.Availability;
using Shora.Contracts.Booking;
using Shora.Contracts.Payments;
using Shora.Domain.Entities;
using Shora.Domain.Enums;
using Shora.Infrastructure.Data;
using Shora.Tests.Common;
using ContractDeliveryMethod = Shora.Contracts.Booking.DeliveryMethod;
using ContractCancellationRequestStatus = Shora.Contracts.Booking.CancellationRequestStatus;

namespace Shora.Tests.Integration.Api;

[Collection("Postgres")]
public class BookingEndpointTests : IDisposable
{
    private readonly AuthWebApplicationFactory _factory;

    public BookingEndpointTests(PostgresFixture sqlServer)
    {
        _factory = new AuthWebApplicationFactory(sqlServer);
    }

    [Fact]
    public async Task Reserve_creates_booking_and_claims_slot()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var slotId = await GetOpenSlotIdAsync(cancellationToken);
        var (client, userId) = await CreateVerifiedClientAsync("reserve-success@example.com", cancellationToken);

        var response = await client.PostAsJsonAsync("/api/v1/bookings", new CreateBookingRequest(
            slotId,
            ContractDeliveryMethod.Chat,
            null), cancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<ReserveBookingResponse>(cancellationToken);
        Assert.NotNull(body);
        Assert.NotEqual(Guid.Empty, body!.BookingId);
        Assert.True(body.PaymentInstructions.Amount > 0);
        Assert.Equal("EGP", body.PaymentInstructions.Currency);
        Assert.True(body.PaymentInstructions.ReceiptUploadDeadlineUtc > DateTime.UtcNow);

        await using var scope = _factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var slot = await context.AvailabilitySlots.AsNoTracking().SingleAsync(s => s.Id == slotId, cancellationToken);
        Assert.True(slot.IsBooked);
        Assert.Equal(body.BookingId, slot.BookingId);

        var booking = await context.Bookings.AsNoTracking().SingleAsync(b => b.Id == body.BookingId, cancellationToken);
        Assert.Equal(userId, booking.ClientId);
        Assert.Equal(BookingStatus.PendingPayment, booking.Status);
        Assert.Equal(slotId, booking.AvailabilitySlotId);

        var payment = await context.Payments.AsNoTracking().SingleAsync(p => p.BookingId == body.BookingId, cancellationToken);
        Assert.Equal(PaymentStatus.AwaitingReceipt, payment.Status);
        Assert.Equal(body.PaymentInstructions.Amount, payment.Amount);

        var audit = await context.BookingStatusAudits.AsNoTracking().SingleAsync(a => a.BookingId == body.BookingId, cancellationToken);
        Assert.Equal(BookingStatus.PendingPayment, audit.ToStatus);
        Assert.Equal(AuditActor.Client, audit.Actor);
    }

    [Fact]
    public async Task Reserve_returns_conflict_when_slot_already_taken()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var slotId = await GetOpenSlotIdAsync(cancellationToken);
        var (firstClient, _) = await CreateVerifiedClientAsync("reserve-first@example.com", cancellationToken);
        var (secondClient, _) = await CreateVerifiedClientAsync("reserve-second@example.com", cancellationToken);

        var firstResponse = await firstClient.PostAsJsonAsync("/api/v1/bookings", new CreateBookingRequest(
            slotId,
            ContractDeliveryMethod.Chat,
            null), cancellationToken);
        Assert.Equal(HttpStatusCode.OK, firstResponse.StatusCode);

        var secondResponse = await secondClient.PostAsJsonAsync("/api/v1/bookings", new CreateBookingRequest(
            slotId,
            ContractDeliveryMethod.Chat,
            null), cancellationToken);

        Assert.Equal(HttpStatusCode.Conflict, secondResponse.StatusCode);
        await AssertProblemCodeAsync(secondResponse, "booking.slot_unavailable", cancellationToken);
    }

    [Fact]
    public async Task Reserve_returns_conflict_when_hold_cap_exceeded()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var (client, userId) = await CreateVerifiedClientAsync("reserve-hold-cap@example.com", cancellationToken);
        await SeedUnconfirmedHoldsAsync(userId, 3, cancellationToken);

        var slotId = await GetOpenSlotIdAsync(cancellationToken);
        var response = await client.PostAsJsonAsync("/api/v1/bookings", new CreateBookingRequest(
            slotId,
            ContractDeliveryMethod.Chat,
            null), cancellationToken);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        await AssertProblemCodeAsync(response, "booking.hold_cap_exceeded", cancellationToken);
    }

    [Fact]
    public async Task Reserve_rejects_unverified_email()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var slotId = await GetOpenSlotIdAsync(cancellationToken);
        var client = CreateClient();

        await client.PostAsJsonAsync("/api/v1/auth/signup", new SignUpRequest(
            "reserve-unverified@example.com",
            "Password123!",
            "Unverified Client"), cancellationToken);

        var loginResponse = await client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest(
            "reserve-unverified@example.com",
            "Password123!"), cancellationToken);
        var loginBody = await loginResponse.Content.ReadFromJsonAsync<AuthResponse>(cancellationToken);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", loginBody!.AccessToken);

        var response = await client.PostAsJsonAsync("/api/v1/bookings", new CreateBookingRequest(
            slotId,
            ContractDeliveryMethod.Chat,
            null), cancellationToken);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        await AssertProblemCodeAsync(response, "booking.email_not_verified", cancellationToken);
    }

    [Fact]
    public async Task Reserve_requires_phone_for_voice_call()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var slotId = await GetOpenSlotIdAsync(cancellationToken);
        var (client, _) = await CreateVerifiedClientAsync("reserve-voice@example.com", cancellationToken);

        var response = await client.PostAsJsonAsync("/api/v1/bookings", new CreateBookingRequest(
            slotId,
            ContractDeliveryMethod.VoiceCall,
            null), cancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        await AssertProblemCodeAsync(response, "booking.contact_phone_required", cancellationToken);
    }

    [Fact]
    public async Task Reserve_normalizes_voice_call_phone()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var slotId = await GetOpenSlotIdAsync(cancellationToken);
        var (client, userId) = await CreateVerifiedClientAsync("reserve-voice-ok@example.com", cancellationToken);

        var response = await client.PostAsJsonAsync("/api/v1/bookings", new CreateBookingRequest(
            slotId,
            ContractDeliveryMethod.VoiceCall,
            "01012345678"), cancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        await using var scope = _factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var booking = await context.Bookings.AsNoTracking().SingleAsync(b => b.ClientId == userId, cancellationToken);
        Assert.Equal("+201012345678", booking.ContactPhone);
    }

    [Fact]
    public async Task Reserve_accepts_string_delivery_method_from_frontend_payload()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var slotId = await GetOpenSlotIdAsync(cancellationToken);
        var (client, userId) = await CreateVerifiedClientAsync("reserve-string-enum@example.com", cancellationToken);

        var content = new StringContent(
            $$"""{"availabilitySlotId":"{{slotId}}","deliveryMethod":"Chat","contactPhone":null}""",
            Encoding.UTF8,
            "application/json");
        var response = await client.PostAsync("/api/v1/bookings", content, cancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        await using var scope = _factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var booking = await context.Bookings.AsNoTracking().SingleAsync(b => b.ClientId == userId, cancellationToken);
        Assert.Equal(Domain.Enums.DeliveryMethod.Chat, booking.DeliveryMethod);
    }

    [Fact]
    public async Task Cancel_hold_releases_slot_and_voids_payment_for_pending_payment()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var (client, bookingId, slotId) = await ReserveBookingAsync("cancel-pending-payment@example.com", cancellationToken);

        var response = await client.PostAsync($"/api/v1/bookings/{bookingId}/cancel", null, cancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        await using var scope = _factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var slot = await context.AvailabilitySlots.AsNoTracking().SingleAsync(s => s.Id == slotId, cancellationToken);
        Assert.False(slot.IsBooked);
        Assert.Null(slot.BookingId);

        var booking = await context.Bookings.AsNoTracking().SingleAsync(b => b.Id == bookingId, cancellationToken);
        Assert.Equal(BookingStatus.Cancelled, booking.Status);
        Assert.Null(booking.AvailabilitySlotId);

        var payment = await context.Payments.AsNoTracking().SingleAsync(p => p.BookingId == bookingId, cancellationToken);
        Assert.Equal(PaymentStatus.Void, payment.Status);

        var audit = await context.BookingStatusAudits
            .AsNoTracking()
            .OrderByDescending(a => a.AtUtc)
            .FirstAsync(a => a.BookingId == bookingId, cancellationToken);
        Assert.Equal(BookingStatus.PendingPayment, audit.FromStatus);
        Assert.Equal(BookingStatus.Cancelled, audit.ToStatus);

        var outbox = await context.OutboxMessages.AsNoTracking().SingleAsync(
            m => m.AggregateId == bookingId && m.MessageType == OutboxMessageTypes.ClientBookingCancelledEmail,
            cancellationToken);
        Assert.Equal(OutboxMessageStatus.Pending, outbox.Status);

        var availabilityClient = _factory.CreateClient();
        var from = slot.StartTimeUtc.AddDays(-1);
        var to = slot.EndTimeUtc.AddDays(1);
        var url = $"/api/v1/availability?from={Uri.EscapeDataString(from.ToString("O"))}&to={Uri.EscapeDataString(to.ToString("O"))}";
        var availabilityResponse = await availabilityClient.GetAsync(url, cancellationToken);
        Assert.Equal(HttpStatusCode.OK, availabilityResponse.StatusCode);

        var availability = await availabilityResponse.Content.ReadFromJsonAsync<AvailabilityResponse>(cancellationToken);
        Assert.Contains(availability!.Slots, returned => returned.Id == slotId);
    }

    [Fact]
    public async Task Cancel_hold_releases_slot_for_pending_approval()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var (client, bookingId, slotId) = await ReserveBookingAsync("cancel-pending-approval@example.com", cancellationToken);
        await SetBookingStatusAsync(bookingId, BookingStatus.PendingApproval, PaymentStatus.UnderReview, cancellationToken);

        var response = await client.PostAsync($"/api/v1/bookings/{bookingId}/cancel", null, cancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        await using var scope = _factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var slot = await context.AvailabilitySlots.AsNoTracking().SingleAsync(s => s.Id == slotId, cancellationToken);
        Assert.False(slot.IsBooked);

        var booking = await context.Bookings.AsNoTracking().SingleAsync(b => b.Id == bookingId, cancellationToken);
        Assert.Equal(BookingStatus.Cancelled, booking.Status);

        var payment = await context.Payments.AsNoTracking().SingleAsync(p => p.BookingId == bookingId, cancellationToken);
        Assert.Equal(PaymentStatus.Void, payment.Status);
    }

    [Fact]
    public async Task Cancel_hold_rejects_confirmed_booking()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var (client, bookingId, _) = await ReserveBookingAsync("cancel-confirmed@example.com", cancellationToken);
        await SetBookingStatusAsync(bookingId, BookingStatus.Confirmed, PaymentStatus.Approved, cancellationToken);

        var response = await client.PostAsync($"/api/v1/bookings/{bookingId}/cancel", null, cancellationToken);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        await AssertProblemCodeAsync(response, "booking.invalid_status", cancellationToken);
    }

    [Fact]
    public async Task Cancel_hold_rejects_non_owner()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var (_, bookingId, _) = await ReserveBookingAsync("cancel-owner@example.com", cancellationToken);
        var (otherClient, _) = await CreateVerifiedClientAsync("cancel-non-owner@example.com", cancellationToken);

        var response = await otherClient.PostAsync($"/api/v1/bookings/{bookingId}/cancel", null, cancellationToken);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        await AssertProblemCodeAsync(response, "booking.forbidden", cancellationToken);
    }

    [Fact]
    public async Task Payment_instructions_returns_frozen_amount_for_pending_payment()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var (client, bookingId, _) = await ReserveBookingAsync("payment-instructions@example.com", cancellationToken);

        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var settings = await context.Settings.SingleAsync(s => s.Id == Settings.SingletonId, cancellationToken);
            settings.SessionPrice = 999m;
            await context.SaveChangesAsync(cancellationToken);
        }

        var response = await client.GetAsync($"/api/v1/bookings/{bookingId}/payment-instructions", cancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<PaymentInstructionsResponse>(cancellationToken);
        Assert.NotNull(body);
        Assert.Equal(500m, body!.Amount);
        Assert.Equal("EGP", body.Currency);
        Assert.Equal("01000000000", body.VodafoneCashNumber);
        Assert.Equal("consultant@instapay", body.InstaPayHandle);
        Assert.True(body.ReceiptUploadDeadlineUtc > DateTime.UtcNow);

        await using var verifyScope = _factory.Services.CreateAsyncScope();
        var verifyContext = verifyScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var payment = await verifyContext.Payments.AsNoTracking().SingleAsync(p => p.BookingId == bookingId, cancellationToken);
        Assert.Equal(body.Amount, payment.Amount);
    }

    [Fact]
    public async Task Payment_instructions_rejects_wrong_status()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var (client, bookingId, _) = await ReserveBookingAsync("payment-instructions-status@example.com", cancellationToken);
        await SetBookingStatusAsync(bookingId, BookingStatus.PendingApproval, PaymentStatus.UnderReview, cancellationToken);

        var response = await client.GetAsync($"/api/v1/bookings/{bookingId}/payment-instructions", cancellationToken);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        await AssertProblemCodeAsync(response, "booking.invalid_status", cancellationToken);
    }

    [Fact]
    public async Task Payment_instructions_rejects_non_owner()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var (_, bookingId, _) = await ReserveBookingAsync("payment-instructions-owner@example.com", cancellationToken);
        var (otherClient, _) = await CreateVerifiedClientAsync("payment-instructions-non-owner@example.com", cancellationToken);

        var response = await otherClient.GetAsync($"/api/v1/bookings/{bookingId}/payment-instructions", cancellationToken);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        await AssertProblemCodeAsync(response, "booking.forbidden", cancellationToken);
    }

    [Fact]
    public async Task Cancellation_request_creates_pending_request_for_confirmed_booking()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var (client, bookingId) = await CreateConfirmedBookingAsync("cancellation-happy@example.com", cancellationToken);

        var response = await client.PostAsJsonAsync(
            $"/api/v1/bookings/{bookingId}/cancellation-requests",
            new CancellationRequestBody("Schedule conflict"),
            cancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<CancellationRequestResponse>(cancellationToken);
        Assert.NotNull(body);
        Assert.NotEqual(Guid.Empty, body!.RequestId);
        Assert.Equal(ContractCancellationRequestStatus.Pending, body.Status);
        Assert.Equal(nameof(BookingStatus.CancellationRequested), body.BookingStatus);
        Assert.True(body.AutoDeclineAtUtc > DateTime.UtcNow);

        await using var scope = _factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var booking = await context.Bookings.AsNoTracking().SingleAsync(b => b.Id == bookingId, cancellationToken);
        Assert.Equal(BookingStatus.CancellationRequested, booking.Status);

        var request = await context.CancellationRequests.AsNoTracking().SingleAsync(r => r.BookingId == bookingId, cancellationToken);
        Assert.Equal(ContractCancellationRequestStatus.Pending, (ContractCancellationRequestStatus)request.Status);
        Assert.Equal("Schedule conflict", request.ClientReason);
        Assert.Equal(0, request.ReopenCount);

        var audit = await context.BookingStatusAudits
            .AsNoTracking()
            .OrderByDescending(a => a.AtUtc)
            .FirstAsync(a => a.BookingId == bookingId, cancellationToken);
        Assert.Equal(BookingStatus.Confirmed, audit.FromStatus);
        Assert.Equal(BookingStatus.CancellationRequested, audit.ToStatus);

        var outbox = await context.OutboxMessages.AsNoTracking().SingleAsync(
            m => m.AggregateId == bookingId && m.MessageType == OutboxMessageTypes.AdminNewCancellationRequestEmail,
            cancellationToken);
        Assert.Equal(OutboxMessageStatus.Pending, outbox.Status);
    }

    [Fact]
    public async Task Cancellation_request_rejects_when_too_close_to_session()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var (client, bookingId) = await CreateConfirmedBookingAsync("cancellation-too-late@example.com", cancellationToken);
        await SetBookingSlotStartAsync(bookingId, DateTime.UtcNow.AddMinutes(30), cancellationToken);

        var response = await client.PostAsJsonAsync(
            $"/api/v1/bookings/{bookingId}/cancellation-requests",
            new CancellationRequestBody(null),
            cancellationToken);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        await AssertProblemCodeAsync(response, "cancellation.too_late", cancellationToken);

        await using var scope = _factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.False(await context.CancellationRequests.AnyAsync(r => r.BookingId == bookingId, cancellationToken));
    }

    [Fact]
    public async Task Cancellation_request_can_reopen_once_after_decline()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var (client, bookingId) = await CreateConfirmedBookingAsync("cancellation-reopen@example.com", cancellationToken);

        var firstResponse = await client.PostAsJsonAsync(
            $"/api/v1/bookings/{bookingId}/cancellation-requests",
            new CancellationRequestBody("First request"),
            cancellationToken);
        Assert.Equal(HttpStatusCode.OK, firstResponse.StatusCode);

        await SimulateDeclinedCancellationRequestAsync(bookingId, cancellationToken);

        var reopenResponse = await client.PostAsJsonAsync(
            $"/api/v1/bookings/{bookingId}/cancellation-requests",
            new CancellationRequestBody("Second request"),
            cancellationToken);
        Assert.Equal(HttpStatusCode.OK, reopenResponse.StatusCode);

        var body = await reopenResponse.Content.ReadFromJsonAsync<CancellationRequestResponse>(cancellationToken);
        Assert.NotNull(body);
        Assert.Equal(ContractCancellationRequestStatus.Pending, body!.Status);
        Assert.Equal(nameof(BookingStatus.CancellationRequested), body.BookingStatus);

        await using var scope = _factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var request = await context.CancellationRequests.AsNoTracking().SingleAsync(r => r.BookingId == bookingId, cancellationToken);
        Assert.Equal(1, request.ReopenCount);
        Assert.Equal("Second request", request.ClientReason);
        Assert.Equal(BookingStatus.CancellationRequested, (await context.Bookings.AsNoTracking().SingleAsync(b => b.Id == bookingId, cancellationToken)).Status);
    }

    [Fact]
    public async Task Cancellation_request_rejects_second_reopen()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var (client, bookingId) = await CreateConfirmedBookingAsync("cancellation-reopen-exhausted@example.com", cancellationToken);

        var firstResponse = await client.PostAsJsonAsync(
            $"/api/v1/bookings/{bookingId}/cancellation-requests",
            new CancellationRequestBody(null),
            cancellationToken);
        Assert.Equal(HttpStatusCode.OK, firstResponse.StatusCode);

        await SimulateDeclinedCancellationRequestAsync(bookingId, cancellationToken);

        var reopenResponse = await client.PostAsJsonAsync(
            $"/api/v1/bookings/{bookingId}/cancellation-requests",
            new CancellationRequestBody(null),
            cancellationToken);
        Assert.Equal(HttpStatusCode.OK, reopenResponse.StatusCode);

        await SimulateDeclinedCancellationRequestAsync(bookingId, cancellationToken);

        var secondReopenResponse = await client.PostAsJsonAsync(
            $"/api/v1/bookings/{bookingId}/cancellation-requests",
            new CancellationRequestBody(null),
            cancellationToken);

        Assert.Equal(HttpStatusCode.Conflict, secondReopenResponse.StatusCode);
        await AssertProblemCodeAsync(secondReopenResponse, "cancellation.reopen_exhausted", cancellationToken);
    }

    [Fact]
    public async Task Cancellation_decision_seen_sets_timestamp_for_declined_request()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var (client, bookingId) = await CreateConfirmedBookingAsync("cancellation-decision-seen@example.com", cancellationToken);

        var requestResponse = await client.PostAsJsonAsync(
            $"/api/v1/bookings/{bookingId}/cancellation-requests",
            new CancellationRequestBody(null),
            cancellationToken);
        Assert.Equal(HttpStatusCode.OK, requestResponse.StatusCode);

        await SimulateDeclinedCancellationRequestAsync(bookingId, cancellationToken);

        var response = await client.PostAsync($"/api/v1/bookings/{bookingId}/cancellation-requests/decision-seen", null, cancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        await using var scope = _factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var request = await context.CancellationRequests.AsNoTracking().SingleAsync(r => r.BookingId == bookingId, cancellationToken);
        Assert.NotNull(request.ClientDecisionSeenAtUtc);
    }

    [Fact]
    public async Task ListMine_requires_authentication()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var client = CreateClient();

        var response = await client.GetAsync("/api/v1/bookings/mine?status=Past", cancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ListMine_rejects_admin_token()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var adminClient = await CreateAdminClientAsync(cancellationToken);

        var response = await adminClient.GetAsync("/api/v1/bookings/mine?status=Past", cancellationToken);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task ListMine_returns_only_current_client_bookings()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var (clientA, userIdA) = await CreateVerifiedClientAsync("list-mine-client-a@example.com", cancellationToken);
        var (clientB, userIdB) = await CreateVerifiedClientAsync("list-mine-client-b@example.com", cancellationToken);

        await SeedPastBookingAsync(userIdA, FixedPastSlot(-2), cancellationToken);
        await SeedPastBookingAsync(userIdA, FixedPastSlot(-3), cancellationToken);
        await SeedPastBookingAsync(userIdB, FixedPastSlot(-1), cancellationToken);

        var response = await clientA.GetAsync("/api/v1/bookings/mine?status=Past&page=1&pageSize=10", cancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<MyBookingsResponse>(cancellationToken);
        Assert.NotNull(body);
        Assert.Equal(2, body!.TotalCount);

        await using var scope = _factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var returnedIds = body.Items.Select(item => item.BookingId).ToHashSet();
        var ownedIds = await context.Bookings.AsNoTracking()
            .Where(booking => booking.ClientId == userIdA && booking.Status == BookingStatus.Completed)
            .Select(booking => booking.Id)
            .ToListAsync(cancellationToken);
        Assert.Equal(2, ownedIds.Count);
        Assert.True(returnedIds.SetEquals(ownedIds));

        var responseB = await clientB.GetAsync("/api/v1/bookings/mine?status=Past", cancellationToken);
        var bodyB = await responseB.Content.ReadFromJsonAsync<MyBookingsResponse>(cancellationToken);
        Assert.NotNull(bodyB);
        Assert.Single(bodyB!.Items);
    }

    [Fact]
    public async Task ListMine_past_filter_is_paginated_and_ordered_most_recent_first()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var (client, userId) = await CreateVerifiedClientAsync("list-mine-past-page@example.com", cancellationToken);
        var newestId = await SeedPastBookingAsync(userId, FixedPastSlot(-1), cancellationToken);
        var middleId = await SeedPastBookingAsync(userId, FixedPastSlot(-2), cancellationToken);
        var oldestId = await SeedPastBookingAsync(userId, FixedPastSlot(-3), cancellationToken);

        var firstPageResponse = await client.GetAsync("/api/v1/bookings/mine?status=Past&page=1&pageSize=2", cancellationToken);
        Assert.Equal(HttpStatusCode.OK, firstPageResponse.StatusCode);
        var firstPage = await firstPageResponse.Content.ReadFromJsonAsync<MyBookingsResponse>(cancellationToken);
        Assert.NotNull(firstPage);
        Assert.Equal(3, firstPage!.TotalCount);
        Assert.Equal(2, firstPage.Items.Count);
        Assert.Equal([newestId, middleId], firstPage.Items.Select(item => item.BookingId).ToArray());

        var secondPageResponse = await client.GetAsync("/api/v1/bookings/mine?status=Past&page=2&pageSize=2", cancellationToken);
        var secondPage = await secondPageResponse.Content.ReadFromJsonAsync<MyBookingsResponse>(cancellationToken);
        Assert.NotNull(secondPage);
        Assert.Single(secondPage!.Items);
        Assert.Equal(oldestId, secondPage.Items[0].BookingId);
    }

    [Fact]
    public async Task ListMine_upcoming_filter_returns_confirmed_future_bookings()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var (client, userId) = await CreateVerifiedClientAsync("list-mine-upcoming@example.com", cancellationToken);
        var confirmedId = await SeedUpcomingBookingAsync(userId, DateTime.UtcNow.AddDays(2), BookingStatus.Confirmed, cancellationToken);
        await SeedUpcomingBookingAsync(userId, DateTime.UtcNow.AddDays(-1), BookingStatus.Confirmed, cancellationToken);
        await SeedUpcomingBookingAsync(userId, DateTime.UtcNow.AddDays(3), BookingStatus.PendingPayment, cancellationToken);

        var response = await client.GetAsync("/api/v1/bookings/mine?status=Upcoming", cancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<MyBookingsResponse>(cancellationToken);
        Assert.NotNull(body);
        Assert.Single(body!.Items);
        Assert.Equal(confirmedId, body.Items[0].BookingId);
        Assert.Equal(nameof(BookingStatus.Confirmed), body.Items[0].Status);
        Assert.NotNull(body.Items[0].ConsultantWhatsAppNumber);
    }

    [Fact]
    public async Task ListMine_past_cancelled_booking_includes_reason_and_refund_labels()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var (client, userId) = await CreateVerifiedClientAsync("list-mine-labels@example.com", cancellationToken);
        var bookingId = await SeedCancelledBookingAsync(
            userId,
            FixedPastSlot(-1),
            AuditActor.Client,
            PaymentStatus.Approved,
            cancellationToken);

        var response = await client.GetAsync("/api/v1/bookings/mine?status=Past", cancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<MyBookingsResponse>(cancellationToken);
        Assert.NotNull(body);
        var item = Assert.Single(body!.Items, i => i.BookingId == bookingId);
        Assert.Equal("Cancelled by you", item.CancellationReasonLabel);
        Assert.Equal("Refund being processed", item.RefundLabel);
    }

    [Fact]
    public async Task ListMine_upcoming_includes_declined_cancellation_request_metadata()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var (client, bookingId) = await CreateConfirmedBookingAsync("list-mine-decline-meta@example.com", cancellationToken);

        var requestResponse = await client.PostAsJsonAsync(
            $"/api/v1/bookings/{bookingId}/cancellation-requests",
            new CancellationRequestBody(null),
            cancellationToken);
        Assert.Equal(HttpStatusCode.OK, requestResponse.StatusCode);

        await SimulateDeclinedCancellationRequestAsync(bookingId, cancellationToken);

        var response = await client.GetAsync("/api/v1/bookings/mine?status=Upcoming", cancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<MyBookingsResponse>(cancellationToken);
        Assert.NotNull(body);
        var item = Assert.Single(body!.Items, i => i.BookingId == bookingId);
        Assert.NotNull(item.CancellationRequest);
        Assert.Equal(ContractCancellationRequestStatus.Declined, item.CancellationRequest!.Status);
        Assert.Equal("Session stands", item.CancellationRequest.DeclineReason);
        Assert.Null(item.CancellationRequest.ClientDecisionSeenAtUtc);
    }

    [Fact]
    public async Task ListMine_rejects_invalid_page()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var (client, _) = await CreateVerifiedClientAsync("list-mine-invalid-page@example.com", cancellationToken);

        var response = await client.GetAsync("/api/v1/bookings/mine?status=Past&page=0", cancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        await AssertProblemCodeAsync(response, ErrorCodes.General.Validation, cancellationToken);
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
        var slot = await context.AvailabilitySlots.AsNoTracking().FirstAsync(s => !s.IsBooked, cancellationToken);
        return slot.Id;
    }

    private async Task SeedUnconfirmedHoldsAsync(Guid clientId, int count, CancellationToken cancellationToken)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var now = DateTime.UtcNow;

        var slots = await context.AvailabilitySlots
            .Where(slot => !slot.IsBooked)
            .OrderBy(slot => slot.StartTimeUtc)
            .Take(count)
            .ToListAsync(cancellationToken);

        foreach (var slot in slots)
        {
            var bookingId = Guid.NewGuid();
            slot.IsBooked = true;
            slot.BookingId = bookingId;

            context.Bookings.Add(new Booking
            {
                Id = bookingId,
                ClientId = clientId,
                AvailabilitySlotId = slot.Id,
                SlotStartUtc = slot.StartTimeUtc,
                SlotEndUtc = slot.EndTimeUtc,
                DeliveryMethod = Domain.Enums.DeliveryMethod.Chat,
                Status = BookingStatus.PendingPayment,
                ReceiptUploadDeadlineUtc = now.AddHours(1),
                CreatedAt = now
            });

            context.Payments.Add(new Payment
            {
                Id = Guid.NewGuid(),
                BookingId = bookingId,
                Status = PaymentStatus.AwaitingReceipt,
                Amount = 500m,
                Currency = "EGP",
                CreatedAt = now,
                UpdatedAt = now
            });
        }

        await context.SaveChangesAsync(cancellationToken);
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

    private async Task SetBookingStatusAsync(
        Guid bookingId,
        BookingStatus bookingStatus,
        PaymentStatus paymentStatus,
        CancellationToken cancellationToken)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var booking = await context.Bookings.Include(b => b.Payment).SingleAsync(b => b.Id == bookingId, cancellationToken);
        booking.Status = bookingStatus;
        if (bookingStatus == BookingStatus.PendingApproval)
        {
            booking.ReceiptUploadDeadlineUtc = null;
        }

        if (booking.Payment is not null)
        {
            booking.Payment.Status = paymentStatus;
            booking.Payment.UpdatedAt = DateTime.UtcNow;
        }

        await context.SaveChangesAsync(cancellationToken);
    }

    private async Task<(HttpClient Client, Guid BookingId)> CreateConfirmedBookingAsync(
        string email,
        CancellationToken cancellationToken)
    {
        var (client, bookingId, _) = await ReserveBookingAsync(email, cancellationToken);
        await SetBookingStatusAsync(bookingId, BookingStatus.Confirmed, PaymentStatus.Approved, cancellationToken);
        return (client, bookingId);
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

    private async Task SimulateDeclinedCancellationRequestAsync(Guid bookingId, CancellationToken cancellationToken)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var booking = await context.Bookings
            .Include(b => b.CancellationRequest)
            .SingleAsync(b => b.Id == bookingId, cancellationToken);

        Assert.NotNull(booking.CancellationRequest);

        booking.Status = BookingStatus.Confirmed;
        booking.CancellationRequest!.Status = Domain.Enums.CancellationRequestStatus.Declined;
        booking.CancellationRequest.ReviewedAtUtc = DateTime.UtcNow;
        booking.CancellationRequest.DecisionReasonCode = DecisionReasonCode.Policy;
        booking.CancellationRequest.DecisionReason = "Session stands";

        await context.SaveChangesAsync(cancellationToken);
    }

    private static async Task AssertProblemCodeAsync(
        HttpResponseMessage response,
        string expectedCode,
        CancellationToken cancellationToken)
    {
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetailsWithCode>(cancellationToken);
        Assert.NotNull(problem);
        Assert.Equal(expectedCode, problem!.Code);
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

    private static DateTime FixedPastSlot(int daysOffset) =>
        DateTime.UtcNow.Date.AddDays(daysOffset).AddHours(14);

    private async Task<Guid> SeedPastBookingAsync(
        Guid clientId,
        DateTime slotStartUtc,
        CancellationToken cancellationToken)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var bookingId = Guid.NewGuid();

        context.Bookings.Add(new Booking
        {
            Id = bookingId,
            ClientId = clientId,
            SlotStartUtc = slotStartUtc,
            SlotEndUtc = slotStartUtc.AddHours(1),
            DeliveryMethod = Domain.Enums.DeliveryMethod.Chat,
            Status = BookingStatus.Completed,
            CreatedAt = slotStartUtc
        });

        await context.SaveChangesAsync(cancellationToken);
        return bookingId;
    }

    private async Task<Guid> SeedUpcomingBookingAsync(
        Guid clientId,
        DateTime slotStartUtc,
        BookingStatus status,
        CancellationToken cancellationToken)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var bookingId = Guid.NewGuid();

        context.Bookings.Add(new Booking
        {
            Id = bookingId,
            ClientId = clientId,
            SlotStartUtc = slotStartUtc,
            SlotEndUtc = slotStartUtc.AddHours(1),
            DeliveryMethod = Domain.Enums.DeliveryMethod.Chat,
            Status = status,
            CreatedAt = DateTime.UtcNow
        });

        await context.SaveChangesAsync(cancellationToken);
        return bookingId;
    }

    private async Task<Guid> SeedCancelledBookingAsync(
        Guid clientId,
        DateTime slotStartUtc,
        AuditActor actor,
        PaymentStatus paymentStatus,
        CancellationToken cancellationToken)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var bookingId = Guid.NewGuid();
        var now = DateTime.UtcNow;

        context.Bookings.Add(new Booking
        {
            Id = bookingId,
            ClientId = clientId,
            SlotStartUtc = slotStartUtc,
            SlotEndUtc = slotStartUtc.AddHours(1),
            DeliveryMethod = Domain.Enums.DeliveryMethod.Chat,
            Status = BookingStatus.Cancelled,
            CreatedAt = now
        });

        context.Payments.Add(new Payment
        {
            Id = Guid.NewGuid(),
            BookingId = bookingId,
            Status = paymentStatus,
            Amount = 500m,
            Currency = "EGP",
            CreatedAt = now,
            UpdatedAt = now
        });

        context.BookingStatusAudits.Add(new BookingStatusAudit
        {
            Id = Guid.NewGuid(),
            BookingId = bookingId,
            FromStatus = BookingStatus.Confirmed,
            ToStatus = BookingStatus.Cancelled,
            Actor = actor,
            AtUtc = now
        });

        await context.SaveChangesAsync(cancellationToken);
        return bookingId;
    }

    private sealed record ProblemDetailsWithCode(string? Code);
}
