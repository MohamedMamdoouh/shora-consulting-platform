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
using ContractDeclineReasonCode = Shora.Contracts.Payments.ReceiptDeclineReasonCode;

namespace Shora.Tests.Integration.Api;

[Collection("Postgres")]
public class AdminReceiptEndpointTests : IDisposable
{
    private readonly AuthWebApplicationFactory _factory;

    public AdminReceiptEndpointTests(PostgresFixture sqlServer)
    {
        _factory = new AuthWebApplicationFactory(sqlServer);
    }

    [Fact]
    public async Task Get_receipts_returns_attempt_history_with_read_url()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var (client, bookingId, _) = await ReserveBookingAsync("admin-receipts@example.com", cancellationToken);

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

        var adminClient = await CreateAdminClientAsync(cancellationToken);
        var response = await adminClient.GetAsync(
            $"/api/v1/admin/bookings/{bookingId}/receipts",
            cancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadApiJsonAsync<AdminBookingReceiptsResponse>(cancellationToken);
        Assert.NotNull(body);
        Assert.Equal(bookingId, body!.BookingId);
        Assert.Equal(nameof(PaymentStatus.UnderReview), body.PaymentStatus);
        Assert.Equal(ContractPaymentMethod.VodafoneCash, body.Method);
        Assert.Equal(500m, body.Amount);
        Assert.Equal("EGP", body.Currency);
        Assert.Single(body.Receipts);

        var receipt = body.Receipts[0];
        Assert.Equal(1, receipt.AttemptNumber);
        Assert.Equal("receipt.jpg", receipt.OriginalFileName);
        Assert.Equal(nameof(ReceiptReviewStatus.Pending), receipt.ReviewStatus);
        Assert.Equal(nameof(BlobState.Finalized), receipt.BlobState);
        Assert.Equal(nameof(MalwareScanStatus.Clean), receipt.MalwareScanStatus);
        Assert.Equal("01012345678", receipt.SenderReference);
        Assert.NotNull(receipt.ImageReadUrl);
        Assert.StartsWith("memory://", receipt.ImageReadUrl, StringComparison.Ordinal);
        Assert.NotNull(receipt.ImageReadUrlExpiresAtUtc);
        Assert.True(receipt.ImageReadUrlExpiresAtUtc > DateTime.UtcNow);
    }

    [Fact]
    public async Task Get_receipts_omits_read_url_when_blob_is_not_finalized()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var (client, bookingId, _) = await ReserveBookingAsync("admin-receipts-pending-blob@example.com", cancellationToken);

        using var uploadContent = CreateReceiptUploadContent(
            ReceiptTestFiles.MinimalJpeg,
            "image/jpeg",
            "receipt.jpg",
            ContractPaymentMethod.InstaPay,
            null);
        var uploadResponse = await client.PostAsync(
            $"/api/v1/payments/{bookingId}/receipt",
            uploadContent,
            cancellationToken);
        Assert.Equal(HttpStatusCode.OK, uploadResponse.StatusCode);

        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var payment = await context.Payments.SingleAsync(p => p.BookingId == bookingId, cancellationToken);
            var receipt = await context.PaymentReceipts.SingleAsync(r => r.PaymentId == payment.Id, cancellationToken);
            receipt.BlobState = BlobState.BlobFinalizePending;
            await context.SaveChangesAsync(cancellationToken);
        }

        var adminClient = await CreateAdminClientAsync(cancellationToken);
        var response = await adminClient.GetAsync(
            $"/api/v1/admin/bookings/{bookingId}/receipts",
            cancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadApiJsonAsync<AdminBookingReceiptsResponse>(cancellationToken);
        Assert.NotNull(body);
        Assert.Single(body!.Receipts);
        Assert.Null(body.Receipts[0].ImageReadUrl);
        Assert.Null(body.Receipts[0].ImageReadUrlExpiresAtUtc);
    }

    [Fact]
    public async Task Get_receipts_omits_read_url_when_finalized_blob_is_missing()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var (client, bookingId, _) = await ReserveBookingAsync("admin-receipts-missing-blob@example.com", cancellationToken);
        await UploadReceiptAsync(client, bookingId, cancellationToken);

        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var fileStorage = scope.ServiceProvider.GetRequiredService<IFileStorage>();
            var payment = await context.Payments.SingleAsync(p => p.BookingId == bookingId, cancellationToken);
            var receipt = await context.PaymentReceipts.SingleAsync(r => r.PaymentId == payment.Id, cancellationToken);
            await fileStorage.DeleteAsync(receipt.BlobPath, cancellationToken);
        }

        var adminClient = await CreateAdminClientAsync(cancellationToken);
        var response = await adminClient.GetAsync(
            $"/api/v1/admin/bookings/{bookingId}/receipts",
            cancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadApiJsonAsync<AdminBookingReceiptsResponse>(cancellationToken);
        Assert.NotNull(body);
        Assert.Single(body!.Receipts);
        Assert.Null(body.Receipts[0].ImageReadUrl);
        Assert.Null(body.Receipts[0].ImageReadUrlExpiresAtUtc);
    }

    [Fact]
    public async Task Get_receipts_rejects_non_admin()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var (client, bookingId, _) = await ReserveBookingAsync("admin-receipts-forbidden@example.com", cancellationToken);

        var response = await client.GetAsync(
            $"/api/v1/admin/bookings/{bookingId}/receipts",
            cancellationToken);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Approve_receipt_confirms_booking_and_enqueues_emails()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var (client, bookingId, _) = await ReserveBookingAsync("admin-approve@example.com", cancellationToken);
        await UploadReceiptAsync(client, bookingId, cancellationToken);

        var adminClient = await CreateAdminClientAsync(cancellationToken);
        var response = await adminClient.PostAsync(
            $"/api/v1/admin/bookings/{bookingId}/receipts/approve",
            null,
            cancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadApiJsonAsync<AdminReceiptDecisionResponse>(cancellationToken);
        Assert.NotNull(body);
        Assert.Equal(bookingId, body!.BookingId);
        Assert.Equal(nameof(BookingStatus.Confirmed), body.BookingStatus);
        Assert.Equal(nameof(PaymentStatus.Approved), body.PaymentStatus);
        Assert.Equal(nameof(ReceiptReviewStatus.Approved), body.ReceiptReviewStatus);
        Assert.Null(body.ReceiptUploadDeadlineUtc);

        await using var scope = _factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var booking = await context.Bookings.AsNoTracking().SingleAsync(b => b.Id == bookingId, cancellationToken);
        Assert.Equal(BookingStatus.Confirmed, booking.Status);

        var payment = await context.Payments.AsNoTracking().SingleAsync(p => p.BookingId == bookingId, cancellationToken);
        Assert.Equal(PaymentStatus.Approved, payment.Status);

        var receipt = await context.PaymentReceipts.AsNoTracking().SingleAsync(r => r.PaymentId == payment.Id, cancellationToken);
        Assert.Equal(ReceiptReviewStatus.Approved, receipt.ReviewStatus);
        Assert.NotNull(receipt.ReviewedAtUtc);

        var audit = await context.BookingStatusAudits
            .AsNoTracking()
            .OrderByDescending(a => a.AtUtc)
            .FirstAsync(a => a.BookingId == bookingId, cancellationToken);
        Assert.Equal(BookingStatus.PendingApproval, audit.FromStatus);
        Assert.Equal(BookingStatus.Confirmed, audit.ToStatus);
        Assert.Equal(AuditActor.Admin, audit.Actor);

        var outboxTypes = await context.OutboxMessages
            .AsNoTracking()
            .Where(m => m.AggregateId == bookingId)
            .Select(m => m.MessageType)
            .ToListAsync(cancellationToken);

        Assert.Contains(OutboxMessageTypes.ClientBookingConfirmedEmail, outboxTypes);
        Assert.Contains(OutboxMessageTypes.AdminNewBookingEmail, outboxTypes);
    }

    [Fact]
    public async Task Approve_receipt_rejects_receipt_that_is_not_reviewable()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var (client, bookingId, _) = await ReserveBookingAsync("admin-approve-unreviewable@example.com", cancellationToken);
        await UploadReceiptAsync(client, bookingId, cancellationToken);

        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var payment = await context.Payments.SingleAsync(p => p.BookingId == bookingId, cancellationToken);
            var receipt = await context.PaymentReceipts.SingleAsync(r => r.PaymentId == payment.Id, cancellationToken);
            receipt.BlobState = BlobState.BlobFinalizePending;
            receipt.MalwareScanStatus = MalwareScanStatus.Pending;
            await context.SaveChangesAsync(cancellationToken);
        }

        var adminClient = await CreateAdminClientAsync(cancellationToken);
        var response = await adminClient.PostAsync(
            $"/api/v1/admin/bookings/{bookingId}/receipts/approve",
            null,
            cancellationToken);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        await AssertProblemCodeAsync(response, "payment.receipt_not_reviewable", cancellationToken);

        await using var verifyScope = _factory.Services.CreateAsyncScope();
        var verifyContext = verifyScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var booking = await verifyContext.Bookings.AsNoTracking().SingleAsync(b => b.Id == bookingId, cancellationToken);
        var verifyPayment = await verifyContext.Payments.AsNoTracking().SingleAsync(p => p.BookingId == bookingId, cancellationToken);

        Assert.Equal(BookingStatus.PendingApproval, booking.Status);
        Assert.Equal(PaymentStatus.UnderReview, verifyPayment.Status);
    }

    [Fact]
    public async Task Decline_receipt_reopens_upload_window_and_enqueues_email()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var (client, bookingId, _) = await ReserveBookingAsync("admin-decline@example.com", cancellationToken);
        await UploadReceiptAsync(client, bookingId, cancellationToken);

        var adminClient = await CreateAdminClientAsync(cancellationToken);
        var response = await adminClient.PostApiJsonAsync(
            $"/api/v1/admin/bookings/{bookingId}/receipts/decline",
            new { reasonCode = "UnreadableImage", reasonNote = "Image is blurry" },
            cancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadApiJsonAsync<AdminReceiptDecisionResponse>(cancellationToken);
        Assert.NotNull(body);
        Assert.Equal(bookingId, body!.BookingId);
        Assert.Equal(nameof(BookingStatus.PendingPayment), body.BookingStatus);
        Assert.Equal(nameof(PaymentStatus.AwaitingReceipt), body.PaymentStatus);
        Assert.Equal(nameof(ReceiptReviewStatus.Declined), body.ReceiptReviewStatus);
        Assert.NotNull(body.ReceiptUploadDeadlineUtc);
        Assert.True(body.ReceiptUploadDeadlineUtc > DateTime.UtcNow);

        await using var scope = _factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var booking = await context.Bookings.AsNoTracking().SingleAsync(b => b.Id == bookingId, cancellationToken);
        Assert.Equal(BookingStatus.PendingPayment, booking.Status);
        Assert.NotNull(booking.ReceiptUploadDeadlineUtc);
        Assert.True(booking.ReceiptUploadDeadlineUtc > DateTime.UtcNow);

        var payment = await context.Payments.AsNoTracking().SingleAsync(p => p.BookingId == bookingId, cancellationToken);
        Assert.Equal(PaymentStatus.AwaitingReceipt, payment.Status);

        var receipt = await context.PaymentReceipts.AsNoTracking().SingleAsync(r => r.PaymentId == payment.Id, cancellationToken);
        Assert.Equal(ReceiptReviewStatus.Declined, receipt.ReviewStatus);
        Assert.Equal(Domain.Enums.DeclineReasonCode.UnreadableImage, receipt.DeclineReasonCode);
        Assert.Equal("Image is blurry", receipt.DeclineReason);

        var outbox = await context.OutboxMessages.AsNoTracking().SingleAsync(
            m => m.AggregateId == bookingId && m.MessageType == OutboxMessageTypes.ClientReceiptDeclinedEmail,
            cancellationToken);
        Assert.Equal(OutboxMessageStatus.Pending, outbox.Status);
    }

    [Fact]
    public async Task Concurrent_approve_and_decline_returns_one_conflict()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var (client, bookingId, _) = await ReserveBookingAsync("admin-concurrent@example.com", cancellationToken);
        await UploadReceiptAsync(client, bookingId, cancellationToken);

        var adminClient1 = await CreateAdminClientAsync(cancellationToken);
        var adminClient2 = await CreateAdminClientAsync(cancellationToken);

        var approveTask = adminClient1.PostAsync(
            $"/api/v1/admin/bookings/{bookingId}/receipts/approve",
            null,
            cancellationToken);

        var declineTask = adminClient2.PostApiJsonAsync(
            $"/api/v1/admin/bookings/{bookingId}/receipts/decline",
            new DeclineReceiptRequest(ContractDeclineReasonCode.Other, null),
            cancellationToken);

        await Task.WhenAll(approveTask, declineTask);

        var approveResponse = await approveTask;
        var declineResponse = await declineTask;

        var statusCodes = new[] { approveResponse.StatusCode, declineResponse.StatusCode };
        Assert.Contains(HttpStatusCode.OK, statusCodes);
        Assert.Contains(HttpStatusCode.Conflict, statusCodes);

        await using var scope = _factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var booking = await context.Bookings.AsNoTracking().SingleAsync(b => b.Id == bookingId, cancellationToken);

        Assert.True(
            booking.Status is BookingStatus.Confirmed or BookingStatus.PendingPayment,
            $"Unexpected final booking status: {booking.Status}");
    }

    [Fact]
    public async Task Decline_then_reupload_creates_second_receipt_attempt()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var (client, bookingId, _) = await ReserveBookingAsync("admin-reupload@example.com", cancellationToken);
        await UploadReceiptAsync(client, bookingId, cancellationToken);

        var adminClient = await CreateAdminClientAsync(cancellationToken);
        var declineResponse = await adminClient.PostApiJsonAsync(
            $"/api/v1/admin/bookings/{bookingId}/receipts/decline",
            new DeclineReceiptRequest(ContractDeclineReasonCode.AmountMismatch, "Sent 400 EGP"),
            cancellationToken);
        Assert.Equal(HttpStatusCode.OK, declineResponse.StatusCode);

        using var secondUpload = CreateReceiptUploadContent(
            ReceiptTestFiles.MinimalJpeg,
            "image/jpeg",
            "receipt-2.jpg",
            ContractPaymentMethod.InstaPay,
            "01098765432");
        var uploadResponse = await client.PostAsync(
            $"/api/v1/payments/{bookingId}/receipt",
            secondUpload,
            cancellationToken);
        Assert.Equal(HttpStatusCode.OK, uploadResponse.StatusCode);

        var historyResponse = await adminClient.GetAsync(
            $"/api/v1/admin/bookings/{bookingId}/receipts",
            cancellationToken);
        Assert.Equal(HttpStatusCode.OK, historyResponse.StatusCode);

        var history = await historyResponse.Content.ReadApiJsonAsync<AdminBookingReceiptsResponse>(cancellationToken);
        Assert.NotNull(history);
        Assert.Equal(2, history!.Receipts.Count);
        Assert.Equal(nameof(ReceiptReviewStatus.Declined), history.Receipts[0].ReviewStatus);
        Assert.Equal(nameof(ReceiptReviewStatus.Pending), history.Receipts[1].ReviewStatus);
        Assert.Equal(1, history.Receipts[0].AttemptNumber);
        Assert.Equal(2, history.Receipts[1].AttemptNumber);
    }

    [Fact]
    public async Task Approve_receipt_rejects_non_admin()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var (client, bookingId, _) = await ReserveBookingAsync("admin-approve-forbidden@example.com", cancellationToken);
        await UploadReceiptAsync(client, bookingId, cancellationToken);

        var response = await client.PostAsync(
            $"/api/v1/admin/bookings/{bookingId}/receipts/approve",
            null,
            cancellationToken);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Get_receipts_returns_not_found_for_missing_booking()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var adminClient = await CreateAdminClientAsync(cancellationToken);

        var response = await adminClient.GetAsync(
            $"/api/v1/admin/bookings/{Guid.NewGuid()}/receipts",
            cancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        await AssertProblemCodeAsync(response, "booking.not_found", cancellationToken);
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

    private async Task UploadReceiptAsync(
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
