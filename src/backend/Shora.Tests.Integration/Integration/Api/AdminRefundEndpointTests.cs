using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shora.Application.Common;
using Shora.Contracts.Auth;
using Shora.Contracts.Booking;
using Shora.Contracts.Payments;
using Shora.Domain.Entities;
using Shora.Domain.Enums;
using Shora.Infrastructure.Data;
using Shora.Tests.Common;
using ContractDeliveryMethod = Shora.Contracts.Booking.DeliveryMethod;
using ContractPaymentMethod = Shora.Contracts.Payments.PaymentMethod;

namespace Shora.Tests.Integration.Api;

[Collection("Postgres")]
public class AdminRefundEndpointTests : IDisposable
{
    private readonly AuthWebApplicationFactory _factory;

    public AdminRefundEndpointTests(PostgresFixture sqlServer)
    {
        _factory = new AuthWebApplicationFactory(sqlServer);
    }

    [Fact]
    public async Task Record_refund_marks_payment_refunded_and_enqueues_email()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var (paymentId, bookingId) = await CreateRefundDuePaymentAsync("refund-record@example.com", cancellationToken);

        var adminClient = await CreateAdminClientAsync(cancellationToken);
        var response = await adminClient.PostApiJsonAsync(
            $"/api/v1/admin/payments/{paymentId}/refunds/record",
            new RecordRefundRequest("VC-123456", "Manual Vodafone Cash refund"),
            cancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadApiJsonAsync<PaymentRefundResponse>(cancellationToken);
        Assert.NotNull(body);
        Assert.Equal(paymentId, body!.PaymentId);
        Assert.Equal(bookingId, body.BookingId);
        Assert.Equal(nameof(PaymentStatus.Refunded), body.PaymentStatus);
        Assert.Equal("VC-123456", body.RefundReference);
        Assert.NotNull(body.RefundedAtUtc);

        await using var scope = _factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var payment = await context.Payments.AsNoTracking().SingleAsync(p => p.Id == paymentId, cancellationToken);
        Assert.Equal(PaymentStatus.Refunded, payment.Status);
        Assert.Equal("VC-123456", payment.RefundReference);
        Assert.NotNull(payment.RefundedAtUtc);
        Assert.NotNull(payment.RefundedByAdminId);

        var outbox = await context.OutboxMessages.AsNoTracking().SingleAsync(
            m => m.AggregateId == paymentId && m.MessageType == OutboxMessageTypes.ClientRefundConfirmationEmail,
            cancellationToken);
        Assert.Equal(OutboxMessageStatus.Pending, outbox.Status);
    }

    [Fact]
    public async Task Record_refund_is_idempotent_when_already_refunded()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var (paymentId, _) = await CreateRefundDuePaymentAsync("refund-idempotent@example.com", cancellationToken);

        var adminClient = await CreateAdminClientAsync(cancellationToken);
        var request = new RecordRefundRequest("VC-999999", null);

        var firstResponse = await adminClient.PostApiJsonAsync(
            $"/api/v1/admin/payments/{paymentId}/refunds/record",
            request,
            cancellationToken);
        Assert.Equal(HttpStatusCode.OK, firstResponse.StatusCode);

        var secondResponse = await adminClient.PostApiJsonAsync(
            $"/api/v1/admin/payments/{paymentId}/refunds/record",
            request,
            cancellationToken);
        Assert.Equal(HttpStatusCode.OK, secondResponse.StatusCode);

        await using var scope = _factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var outboxCount = await context.OutboxMessages.AsNoTracking().CountAsync(
            m => m.AggregateId == paymentId && m.MessageType == OutboxMessageTypes.ClientRefundConfirmationEmail,
            cancellationToken);
        Assert.Equal(1, outboxCount);
    }

    [Fact]
    public async Task Record_refund_rejects_non_refund_due_payment()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var (_, bookingId, _) = await ReserveBookingAsync("refund-not-due@example.com", cancellationToken);

        await using var scope = _factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var payment = await context.Payments.AsNoTracking().SingleAsync(p => p.BookingId == bookingId, cancellationToken);

        var adminClient = await CreateAdminClientAsync(cancellationToken);
        var response = await adminClient.PostApiJsonAsync(
            $"/api/v1/admin/payments/{payment.Id}/refunds/record",
            new RecordRefundRequest("VC-000001", null),
            cancellationToken);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        await AssertProblemCodeAsync(response, "payment.refund_not_due", cancellationToken);
    }

    public void Dispose()
    {
        _factory.Dispose();
    }

    private async Task<(Guid PaymentId, Guid BookingId)> CreateRefundDuePaymentAsync(
        string email,
        CancellationToken cancellationToken)
    {
        var (client, bookingId, slotId) = await ReserveBookingAsync(email, cancellationToken);
        await UploadReceiptAsync(client, bookingId, cancellationToken);

        var adminClient = await CreateAdminClientAsync(cancellationToken);
        var approveResponse = await adminClient.PostAsync(
            $"/api/v1/admin/bookings/{bookingId}/receipts/approve",
            null,
            cancellationToken);
        Assert.Equal(HttpStatusCode.OK, approveResponse.StatusCode);

        await using var scope = _factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var booking = await context.Bookings.SingleAsync(b => b.Id == bookingId, cancellationToken);
        booking.Status = BookingStatus.Cancelled;
        booking.AvailabilitySlotId = null;

        var slot = await context.AvailabilitySlots.SingleAsync(s => s.Id == slotId, cancellationToken);
        slot.IsBooked = false;
        slot.BookingId = null;

        var payment = await context.Payments.SingleAsync(p => p.BookingId == bookingId, cancellationToken);
        Assert.Equal(PaymentStatus.Approved, payment.Status);

        await context.SaveChangesAsync(cancellationToken);

        return (payment.Id, bookingId);
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
        var loginResponse = await client.PostApiJsonAsync(
            "/api/v1/auth/login",
            new LoginRequest("admin@test.local", "TestPass123!"),
            cancellationToken);
        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);

        var loginBody = await loginResponse.Content.ReadApiJsonAsync<AuthResponse>(cancellationToken);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", loginBody!.AccessToken);
        return client;
    }

    private async Task<(HttpClient Client, Guid UserId)> CreateVerifiedClientAsync(
        string email,
        CancellationToken cancellationToken)
    {
        var client = CreateClient();

        await client.PostApiJsonAsync("/api/v1/auth/signup", new SignUpRequest(
            email,
            "Password123!",
            "Test Client"), cancellationToken);

        await using var scope = _factory.Services.CreateAsyncScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = await userManager.FindByEmailAsync(email);
        Assert.NotNull(user);

        var token = await userManager.GenerateEmailConfirmationTokenAsync(user!);
        await client.PostApiJsonAsync("/api/v1/auth/verify-email", new VerifyEmailRequest(email, token), cancellationToken);

        var loginResponse = await client.PostApiJsonAsync("/api/v1/auth/login", new LoginRequest(
            email,
            "Password123!"), cancellationToken);
        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);
        var loginBody = await loginResponse.Content.ReadApiJsonAsync<AuthResponse>(cancellationToken);
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

    private async Task<(HttpClient Client, Guid BookingId, Guid SlotId)> ReserveBookingAsync(
        string email,
        CancellationToken cancellationToken)
    {
        var slotId = await GetOpenSlotIdAsync(cancellationToken);
        var (client, _) = await CreateVerifiedClientAsync(email, cancellationToken);

        var response = await client.PostApiJsonAsync("/api/v1/bookings", new CreateBookingRequest(
            slotId,
            ContractDeliveryMethod.Chat,
            null), cancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadApiJsonAsync<ReserveBookingResponse>(cancellationToken);
        return (client, body!.BookingId, slotId);
    }

    private static async Task UploadReceiptAsync(
        HttpClient client,
        Guid bookingId,
        CancellationToken cancellationToken)
    {
        using var uploadContent = CreateReceiptUploadContent(
            ReceiptTestFiles.MinimalJpeg,
            "image/jpeg",
            "receipt.jpg",
            ContractPaymentMethod.VodafoneCash,
            "01012345678");
        var uploadResponse = await client.PostAsync(
            $"/api/v1/payments/{bookingId}/receipt",
            uploadContent,
            cancellationToken);
        Assert.Equal(HttpStatusCode.OK, uploadResponse.StatusCode);
    }

    private static MultipartFormDataContent CreateReceiptUploadContent(
        byte[] fileBytes,
        string contentType,
        string fileName,
        ContractPaymentMethod method,
        string? senderReference)
    {
        var content = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(fileBytes);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        content.Add(fileContent, "image", fileName);
        content.Add(new StringContent(method.ToString()), "method");

        if (!string.IsNullOrWhiteSpace(senderReference))
        {
            content.Add(new StringContent(senderReference), "senderReference");
        }

        return content;
    }

    private static async Task AssertProblemCodeAsync(
        HttpResponseMessage response,
        string expectedCode,
        CancellationToken cancellationToken)
    {
        var problem = await response.Content.ReadApiJsonAsync<ProblemDetailsWithCode>(cancellationToken);
        Assert.NotNull(problem);
        Assert.Equal(expectedCode, problem!.Code);
    }

    private sealed record ProblemDetailsWithCode(string? Code);
}
