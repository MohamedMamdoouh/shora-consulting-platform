using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shora.Contracts.Auth;
using Shora.Contracts.Availability;
using Shora.Contracts.Booking;
using Shora.Application.Common;
using Shora.Domain.Entities;
using Shora.Domain.Enums;
using Shora.Infrastructure.Data;
using Shora.Tests.Common;
using ContractDeliveryMethod = Shora.Contracts.Booking.DeliveryMethod;

namespace Shora.Tests.Integration.Api;

[Collection("SqlServer")]
public class BookingEndpointTests : IDisposable
{
    private readonly AuthWebApplicationFactory _factory;

    public BookingEndpointTests(SqlServerFixture sqlServer)
    {
        _factory = new AuthWebApplicationFactory(sqlServer);
    }

    [Fact]
    public async Task Reserve_creates_booking_and_claims_slot()
    {
        var slotId = await GetOpenSlotIdAsync();
        var (client, userId) = await CreateVerifiedClientAsync("reserve-success@example.com");

        var response = await client.PostAsJsonAsync("/api/v1/bookings", new CreateBookingRequest(
            slotId,
            ContractDeliveryMethod.Chat,
            null));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<ReserveBookingResponse>();
        Assert.NotNull(body);
        Assert.NotEqual(Guid.Empty, body!.BookingId);
        Assert.True(body.PaymentInstructions.Amount > 0);
        Assert.Equal("EGP", body.PaymentInstructions.Currency);
        Assert.True(body.PaymentInstructions.ReceiptUploadDeadlineUtc > DateTime.UtcNow);

        await using var scope = _factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var slot = await context.AvailabilitySlots.AsNoTracking().SingleAsync(s => s.Id == slotId);
        Assert.True(slot.IsBooked);
        Assert.Equal(body.BookingId, slot.BookingId);

        var booking = await context.Bookings.AsNoTracking().SingleAsync(b => b.Id == body.BookingId);
        Assert.Equal(userId, booking.ClientId);
        Assert.Equal(BookingStatus.PendingPayment, booking.Status);
        Assert.Equal(slotId, booking.AvailabilitySlotId);

        var payment = await context.Payments.AsNoTracking().SingleAsync(p => p.BookingId == body.BookingId);
        Assert.Equal(PaymentStatus.AwaitingReceipt, payment.Status);
        Assert.Equal(body.PaymentInstructions.Amount, payment.Amount);

        var audit = await context.BookingStatusAudits.AsNoTracking().SingleAsync(a => a.BookingId == body.BookingId);
        Assert.Equal(BookingStatus.PendingPayment, audit.ToStatus);
        Assert.Equal(AuditActor.Client, audit.Actor);
    }

    [Fact]
    public async Task Reserve_returns_conflict_when_slot_already_taken()
    {
        var slotId = await GetOpenSlotIdAsync();
        var (firstClient, _) = await CreateVerifiedClientAsync("reserve-first@example.com");
        var (secondClient, _) = await CreateVerifiedClientAsync("reserve-second@example.com");

        var firstResponse = await firstClient.PostAsJsonAsync("/api/v1/bookings", new CreateBookingRequest(
            slotId,
            ContractDeliveryMethod.Chat,
            null));
        Assert.Equal(HttpStatusCode.OK, firstResponse.StatusCode);

        var secondResponse = await secondClient.PostAsJsonAsync("/api/v1/bookings", new CreateBookingRequest(
            slotId,
            ContractDeliveryMethod.Chat,
            null));

        Assert.Equal(HttpStatusCode.Conflict, secondResponse.StatusCode);
        await AssertProblemCodeAsync(secondResponse, "booking.slot_unavailable");
    }

    [Fact]
    public async Task Reserve_returns_conflict_when_hold_cap_exceeded()
    {
        var (client, userId) = await CreateVerifiedClientAsync("reserve-hold-cap@example.com");
        await SeedUnconfirmedHoldsAsync(userId, 3);

        var slotId = await GetOpenSlotIdAsync();
        var response = await client.PostAsJsonAsync("/api/v1/bookings", new CreateBookingRequest(
            slotId,
            ContractDeliveryMethod.Chat,
            null));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        await AssertProblemCodeAsync(response, "booking.hold_cap_exceeded");
    }

    [Fact]
    public async Task Reserve_rejects_unverified_email()
    {
        var slotId = await GetOpenSlotIdAsync();
        var client = CreateClient();

        await client.PostAsJsonAsync("/api/v1/auth/signup", new SignUpRequest(
            "reserve-unverified@example.com",
            "Password123!",
            "Unverified Client"));

        var loginResponse = await client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest(
            "reserve-unverified@example.com",
            "Password123!"));
        var loginBody = await loginResponse.Content.ReadFromJsonAsync<AuthResponse>();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", loginBody!.AccessToken);

        var response = await client.PostAsJsonAsync("/api/v1/bookings", new CreateBookingRequest(
            slotId,
            ContractDeliveryMethod.Chat,
            null));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        await AssertProblemCodeAsync(response, "booking.email_not_verified");
    }

    [Fact]
    public async Task Reserve_requires_phone_for_voice_call()
    {
        var slotId = await GetOpenSlotIdAsync();
        var (client, _) = await CreateVerifiedClientAsync("reserve-voice@example.com");

        var response = await client.PostAsJsonAsync("/api/v1/bookings", new CreateBookingRequest(
            slotId,
            ContractDeliveryMethod.VoiceCall,
            null));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        await AssertProblemCodeAsync(response, "booking.contact_phone_required");
    }

    [Fact]
    public async Task Reserve_normalizes_voice_call_phone()
    {
        var slotId = await GetOpenSlotIdAsync();
        var (client, userId) = await CreateVerifiedClientAsync("reserve-voice-ok@example.com");

        var response = await client.PostAsJsonAsync("/api/v1/bookings", new CreateBookingRequest(
            slotId,
            ContractDeliveryMethod.VoiceCall,
            "01012345678"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        await using var scope = _factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var booking = await context.Bookings.AsNoTracking().SingleAsync(b => b.ClientId == userId);
        Assert.Equal("+201012345678", booking.ContactPhone);
    }

    [Fact]
    public async Task Cancel_hold_releases_slot_and_voids_payment_for_pending_payment()
    {
        var (client, bookingId, slotId) = await ReserveBookingAsync("cancel-pending-payment@example.com");

        var response = await client.PostAsync($"/api/v1/bookings/{bookingId}/cancel", null);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        await using var scope = _factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var slot = await context.AvailabilitySlots.AsNoTracking().SingleAsync(s => s.Id == slotId);
        Assert.False(slot.IsBooked);
        Assert.Null(slot.BookingId);

        var booking = await context.Bookings.AsNoTracking().SingleAsync(b => b.Id == bookingId);
        Assert.Equal(BookingStatus.Cancelled, booking.Status);
        Assert.Null(booking.AvailabilitySlotId);

        var payment = await context.Payments.AsNoTracking().SingleAsync(p => p.BookingId == bookingId);
        Assert.Equal(PaymentStatus.Void, payment.Status);

        var audit = await context.BookingStatusAudits
            .AsNoTracking()
            .OrderByDescending(a => a.AtUtc)
            .FirstAsync(a => a.BookingId == bookingId);
        Assert.Equal(BookingStatus.PendingPayment, audit.FromStatus);
        Assert.Equal(BookingStatus.Cancelled, audit.ToStatus);

        var outbox = await context.OutboxMessages.AsNoTracking().SingleAsync(
            m => m.AggregateId == bookingId && m.MessageType == OutboxMessageTypes.ClientBookingCancelledEmail);
        Assert.Equal(OutboxMessageStatus.Pending, outbox.Status);

        var availabilityClient = _factory.CreateClient();
        var from = slot.StartTimeUtc.AddDays(-1);
        var to = slot.EndTimeUtc.AddDays(1);
        var url = $"/api/v1/availability?from={Uri.EscapeDataString(from.ToString("O"))}&to={Uri.EscapeDataString(to.ToString("O"))}";
        var availabilityResponse = await availabilityClient.GetAsync(url);
        Assert.Equal(HttpStatusCode.OK, availabilityResponse.StatusCode);

        var availability = await availabilityResponse.Content.ReadFromJsonAsync<AvailabilityResponse>();
        Assert.Contains(availability!.Slots, returned => returned.Id == slotId);
    }

    [Fact]
    public async Task Cancel_hold_releases_slot_for_pending_approval()
    {
        var (client, bookingId, slotId) = await ReserveBookingAsync("cancel-pending-approval@example.com");
        await SetBookingStatusAsync(bookingId, BookingStatus.PendingApproval, PaymentStatus.UnderReview);

        var response = await client.PostAsync($"/api/v1/bookings/{bookingId}/cancel", null);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        await using var scope = _factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var slot = await context.AvailabilitySlots.AsNoTracking().SingleAsync(s => s.Id == slotId);
        Assert.False(slot.IsBooked);

        var booking = await context.Bookings.AsNoTracking().SingleAsync(b => b.Id == bookingId);
        Assert.Equal(BookingStatus.Cancelled, booking.Status);

        var payment = await context.Payments.AsNoTracking().SingleAsync(p => p.BookingId == bookingId);
        Assert.Equal(PaymentStatus.Void, payment.Status);
    }

    [Fact]
    public async Task Cancel_hold_rejects_confirmed_booking()
    {
        var (client, bookingId, _) = await ReserveBookingAsync("cancel-confirmed@example.com");
        await SetBookingStatusAsync(bookingId, BookingStatus.Confirmed, PaymentStatus.Approved);

        var response = await client.PostAsync($"/api/v1/bookings/{bookingId}/cancel", null);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        await AssertProblemCodeAsync(response, "booking.invalid_status");
    }

    [Fact]
    public async Task Cancel_hold_rejects_non_owner()
    {
        var (_, bookingId, _) = await ReserveBookingAsync("cancel-owner@example.com");
        var (otherClient, _) = await CreateVerifiedClientAsync("cancel-non-owner@example.com");

        var response = await otherClient.PostAsync($"/api/v1/bookings/{bookingId}/cancel", null);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        await AssertProblemCodeAsync(response, "booking.forbidden");
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

    private async Task<(HttpClient Client, Guid UserId)> CreateVerifiedClientAsync(string email)
    {
        var client = CreateClient();

        await client.PostAsJsonAsync("/api/v1/auth/signup", new SignUpRequest(
            email,
            "Password123!",
            "Test Client"));

        await using var scope = _factory.Services.CreateAsyncScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = await userManager.FindByEmailAsync(email);
        Assert.NotNull(user);

        var token = await userManager.GenerateEmailConfirmationTokenAsync(user!);
        await client.PostAsJsonAsync("/api/v1/auth/verify-email", new VerifyEmailRequest(email, token));

        var loginResponse = await client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest(
            email,
            "Password123!"));
        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);
        var loginBody = await loginResponse.Content.ReadFromJsonAsync<AuthResponse>();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", loginBody!.AccessToken);

        return (client, user!.Id);
    }

    private async Task<Guid> GetOpenSlotIdAsync()
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var slot = await context.AvailabilitySlots.AsNoTracking().FirstAsync(s => !s.IsBooked);
        return slot.Id;
    }

    private async Task SeedUnconfirmedHoldsAsync(Guid clientId, int count)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var now = DateTime.UtcNow;

        var slots = await context.AvailabilitySlots
            .Where(slot => !slot.IsBooked)
            .OrderBy(slot => slot.StartTimeUtc)
            .Take(count)
            .ToListAsync();

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

        await context.SaveChangesAsync();
    }

    private async Task<(HttpClient Client, Guid BookingId, Guid SlotId)> ReserveBookingAsync(string email)
    {
        var slotId = await GetOpenSlotIdAsync();
        var (client, _) = await CreateVerifiedClientAsync(email);

        var response = await client.PostAsJsonAsync("/api/v1/bookings", new CreateBookingRequest(
            slotId,
            ContractDeliveryMethod.Chat,
            null));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<ReserveBookingResponse>();
        return (client, body!.BookingId, slotId);
    }

    private async Task SetBookingStatusAsync(
        Guid bookingId,
        BookingStatus bookingStatus,
        PaymentStatus paymentStatus)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var booking = await context.Bookings.Include(b => b.Payment).SingleAsync(b => b.Id == bookingId);
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

        await context.SaveChangesAsync();
    }

    private static async Task AssertProblemCodeAsync(HttpResponseMessage response, string expectedCode)
    {
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetailsWithCode>();
        Assert.NotNull(problem);
        Assert.Equal(expectedCode, problem!.Code);
    }

    private sealed record ProblemDetailsWithCode(string? Code);
}
