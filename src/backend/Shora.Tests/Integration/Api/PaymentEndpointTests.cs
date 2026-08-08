using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shora.Application.Abstractions;
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

[Collection("SqlServer")]
public class PaymentEndpointTests : IDisposable
{
    private readonly AuthWebApplicationFactory _factory;

    public PaymentEndpointTests(SqlServerFixture sqlServer)
    {
        _factory = new AuthWebApplicationFactory(sqlServer);
    }

    [Fact]
    public async Task Upload_receipt_moves_booking_to_pending_approval()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var (client, bookingId, _) = await ReserveBookingAsync("receipt-upload@example.com", cancellationToken);

        using var content = CreateReceiptUploadContent(
            ReceiptTestFiles.MinimalJpeg,
            "image/jpeg",
            "receipt.jpg",
            ContractPaymentMethod.VodafoneCash,
            "01012345678");

        var response = await client.PostAsync($"/api/v1/payments/{bookingId}/receipt", content, cancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<UploadReceiptResponse>(cancellationToken);
        Assert.NotNull(body);
        Assert.NotEqual(Guid.Empty, body!.ReceiptId);
        Assert.Equal(bookingId, body.BookingId);
        Assert.Equal(nameof(BookingStatus.PendingApproval), body.BookingStatus);

        await using var scope = _factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var booking = await context.Bookings.AsNoTracking().SingleAsync(b => b.Id == bookingId, cancellationToken);
        Assert.Equal(BookingStatus.PendingApproval, booking.Status);
        Assert.Null(booking.ReceiptUploadDeadlineUtc);

        var payment = await context.Payments.AsNoTracking().SingleAsync(p => p.BookingId == bookingId, cancellationToken);
        Assert.Equal(PaymentStatus.UnderReview, payment.Status);
        Assert.Equal(Domain.Enums.PaymentMethod.VodafoneCash, payment.Method);

        var receipt = await context.PaymentReceipts.AsNoTracking().SingleAsync(r => r.Id == body.ReceiptId, cancellationToken);
        Assert.Equal(ReceiptReviewStatus.Pending, receipt.ReviewStatus);
        Assert.Equal(BlobState.Finalized, receipt.BlobState);
        Assert.Equal(MalwareScanStatus.Clean, receipt.MalwareScanStatus);
        Assert.False(string.IsNullOrWhiteSpace(receipt.ContentHashSha256));
        Assert.Equal("01012345678", receipt.SenderReference);

        var audit = await context.BookingStatusAudits
            .AsNoTracking()
            .OrderByDescending(a => a.AtUtc)
            .FirstAsync(a => a.BookingId == bookingId, cancellationToken);
        Assert.Equal(BookingStatus.PendingPayment, audit.FromStatus);
        Assert.Equal(BookingStatus.PendingApproval, audit.ToStatus);

        var outbox = await context.OutboxMessages.AsNoTracking().SingleAsync(
            m => m.AggregateId == bookingId && m.MessageType == OutboxMessageTypes.AdminReceiptUploadedEmail,
            cancellationToken);
        Assert.Equal(OutboxMessageStatus.Pending, outbox.Status);

        var fileStorage = (InMemoryFileStorage)scope.ServiceProvider.GetRequiredService<IFileStorage>();
        Assert.True(fileStorage.TryGetBlob(receipt.BlobPath, out var storedBytes));
        Assert.Equal(ReceiptTestFiles.MinimalJpeg, storedBytes);
    }

    [Fact]
    public async Task Upload_receipt_rolls_back_when_blob_finalize_fails()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var (client, bookingId, _) = await ReserveBookingAsync("receipt-finalize-fails@example.com", cancellationToken);

        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var fileStorage = (InMemoryFileStorage)scope.ServiceProvider.GetRequiredService<IFileStorage>();
            fileStorage.FailNextFinalize = true;
        }

        using var content = CreateReceiptUploadContent(
            ReceiptTestFiles.MinimalJpeg,
            "image/jpeg",
            "receipt.jpg",
            ContractPaymentMethod.VodafoneCash,
            null);

        var response = await client.PostAsync($"/api/v1/payments/{bookingId}/receipt", content, cancellationToken);

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        await AssertProblemCodeAsync(response, "payment.receipt_finalize_failed", cancellationToken);

        await using var verifyScope = _factory.Services.CreateAsyncScope();
        var context = verifyScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var booking = await context.Bookings.AsNoTracking().SingleAsync(b => b.Id == bookingId, cancellationToken);
        Assert.Equal(BookingStatus.PendingPayment, booking.Status);
        Assert.NotNull(booking.ReceiptUploadDeadlineUtc);

        var payment = await context.Payments.AsNoTracking().SingleAsync(p => p.BookingId == bookingId, cancellationToken);
        Assert.Equal(PaymentStatus.AwaitingReceipt, payment.Status);

        var receiptCount = await context.PaymentReceipts
            .AsNoTracking()
            .CountAsync(r => r.PaymentId == payment.Id, cancellationToken);
        Assert.Equal(0, receiptCount);
    }

    [Fact]
    public async Task Upload_receipt_rejects_second_upload_while_pending_approval()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var (client, bookingId, _) = await ReserveBookingAsync("receipt-upload-twice@example.com", cancellationToken);

        using var firstUpload = CreateReceiptUploadContent(
            ReceiptTestFiles.MinimalJpeg,
            "image/jpeg",
            "receipt.jpg",
            ContractPaymentMethod.VodafoneCash,
            null);
        var firstResponse = await client.PostAsync($"/api/v1/payments/{bookingId}/receipt", firstUpload, cancellationToken);
        Assert.Equal(HttpStatusCode.OK, firstResponse.StatusCode);

        using var secondUpload = CreateReceiptUploadContent(
            ReceiptTestFiles.MinimalJpeg,
            "image/jpeg",
            "receipt-2.jpg",
            ContractPaymentMethod.InstaPay,
            null);
        var secondResponse = await client.PostAsync($"/api/v1/payments/{bookingId}/receipt", secondUpload, cancellationToken);

        Assert.Equal(HttpStatusCode.Conflict, secondResponse.StatusCode);
        await AssertProblemCodeAsync(secondResponse, "payment.invalid_status", cancellationToken);
    }

    [Fact]
    public async Task Upload_receipt_rejects_expired_deadline()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var (client, bookingId, _) = await ReserveBookingAsync("receipt-upload-expired@example.com", cancellationToken);
        await SetReceiptUploadDeadlineAsync(bookingId, DateTime.UtcNow.AddMinutes(-5), cancellationToken);

        using var content = CreateReceiptUploadContent(
            ReceiptTestFiles.MinimalJpeg,
            "image/jpeg",
            "receipt.jpg",
            ContractPaymentMethod.InstaPay,
            null);

        var response = await client.PostAsync($"/api/v1/payments/{bookingId}/receipt", content, cancellationToken);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        await AssertProblemCodeAsync(response, "payment.upload_deadline_passed", cancellationToken);
    }

    [Fact]
    public async Task Upload_receipt_rejects_non_owner()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var (_, bookingId, _) = await ReserveBookingAsync("receipt-upload-owner@example.com", cancellationToken);
        var (otherClient, _) = await CreateVerifiedClientAsync("receipt-upload-non-owner@example.com", cancellationToken);

        using var content = CreateReceiptUploadContent(
            ReceiptTestFiles.MinimalJpeg,
            "image/jpeg",
            "receipt.jpg",
            ContractPaymentMethod.VodafoneCash,
            null);

        var response = await otherClient.PostAsync($"/api/v1/payments/{bookingId}/receipt", content, cancellationToken);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        await AssertProblemCodeAsync(response, "booking.forbidden", cancellationToken);
    }

    [Fact]
    public async Task Upload_receipt_rejects_invalid_file_type()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var (client, bookingId, _) = await ReserveBookingAsync("receipt-upload-invalid@example.com", cancellationToken);

        using var content = CreateReceiptUploadContent(
            "plain-text"u8.ToArray(),
            "image/png",
            "receipt.png",
            ContractPaymentMethod.VodafoneCash,
            null);

        var response = await client.PostAsync($"/api/v1/payments/{bookingId}/receipt", content, cancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        await AssertProblemCodeAsync(response, "payment.invalid_receipt_file", cancellationToken);
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

    private async Task SetReceiptUploadDeadlineAsync(
        Guid bookingId,
        DateTime deadlineUtc,
        CancellationToken cancellationToken)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var booking = await context.Bookings.SingleAsync(b => b.Id == bookingId, cancellationToken);
        booking.ReceiptUploadDeadlineUtc = deadlineUtc;
        await context.SaveChangesAsync(cancellationToken);
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
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetailsWithCode>(cancellationToken);
        Assert.NotNull(problem);
        Assert.Equal(expectedCode, problem!.Code);
    }

    private sealed record ProblemDetailsWithCode(string? Code);
}
