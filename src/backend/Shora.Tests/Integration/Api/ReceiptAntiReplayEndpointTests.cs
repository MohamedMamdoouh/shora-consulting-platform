using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
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

[Collection("SqlServer")]
public class ReceiptAntiReplayEndpointTests : IDisposable
{
    private readonly AuthWebApplicationFactory _factory;

    public ReceiptAntiReplayEndpointTests(SqlServerFixture sqlServer)
    {
        _factory = new AuthWebApplicationFactory(sqlServer);
    }

    [Fact]
    public async Task Upload_receipt_flags_duplicate_content_hash_across_bookings()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var (firstClient, firstBookingId, _) = await ReserveBookingAsync("anti-replay-first@example.com", cancellationToken);

        using (var firstUpload = CreateReceiptUploadContent(
            ReceiptTestFiles.MinimalJpeg,
            "image/jpeg",
            "receipt.jpg",
            ContractPaymentMethod.VodafoneCash,
            "01011111111"))
        {
            var firstResponse = await firstClient.PostAsync(
                $"/api/v1/payments/{firstBookingId}/receipt",
                firstUpload,
                cancellationToken);
            Assert.Equal(HttpStatusCode.OK, firstResponse.StatusCode);

            var firstBody = await firstResponse.Content.ReadFromJsonAsync<UploadReceiptResponse>(cancellationToken);
            Assert.NotNull(firstBody);
            Assert.Empty(firstBody!.ReviewWarnings);
        }

        var (secondClient, secondBookingId, _) = await ReserveBookingAsync("anti-replay-second@example.com", cancellationToken);
        using var secondUpload = CreateReceiptUploadContent(
            ReceiptTestFiles.MinimalJpeg,
            "image/jpeg",
            "receipt-copy.jpg",
            ContractPaymentMethod.VodafoneCash,
            "01022222222");
        var secondResponse = await secondClient.PostAsync(
            $"/api/v1/payments/{secondBookingId}/receipt",
            secondUpload,
            cancellationToken);

        Assert.Equal(HttpStatusCode.OK, secondResponse.StatusCode);

        var secondBody = await secondResponse.Content.ReadFromJsonAsync<UploadReceiptResponse>(cancellationToken);
        Assert.NotNull(secondBody);
        Assert.Contains(nameof(ReceiptReviewWarning.DuplicateContentHash), secondBody!.ReviewWarnings);

        var adminClient = await CreateAdminClientAsync(cancellationToken);
        var historyResponse = await adminClient.GetAsync(
            $"/api/v1/admin/bookings/{secondBookingId}/receipts",
            cancellationToken);
        Assert.Equal(HttpStatusCode.OK, historyResponse.StatusCode);

        var history = await historyResponse.Content.ReadFromJsonAsync<AdminBookingReceiptsResponse>(cancellationToken);
        Assert.NotNull(history);
        Assert.Single(history!.Receipts);
        Assert.Contains(nameof(ReceiptReviewWarning.DuplicateContentHash), history.Receipts[0].ReviewWarnings);
    }

    [Fact]
    public async Task Sixth_upload_within_a_minute_returns_too_many_requests()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var (client, bookingId, _) = await ReserveBookingAsync("rate-limit@example.com", cancellationToken);
        var adminClient = await CreateAdminClientAsync(cancellationToken);

        for (var attempt = 1; attempt <= 5; attempt++)
        {
            using var upload = CreateReceiptUploadContent(
                ReceiptTestFiles.CreateUniqueJpeg(attempt),
                "image/jpeg",
                $"receipt-{attempt}.jpg",
                ContractPaymentMethod.VodafoneCash,
                $"0109000000{attempt}");
            var uploadResponse = await client.PostAsync(
                $"/api/v1/payments/{bookingId}/receipt",
                upload,
                cancellationToken);
            Assert.Equal(HttpStatusCode.OK, uploadResponse.StatusCode);

            var declineResponse = await adminClient.PostAsJsonAsync(
                $"/api/v1/admin/bookings/{bookingId}/receipts/decline",
                new DeclineReceiptRequest(ContractDeclineReasonCode.Other, null),
                cancellationToken);
            Assert.Equal(HttpStatusCode.OK, declineResponse.StatusCode);
        }

        using var sixthUpload = CreateReceiptUploadContent(
            ReceiptTestFiles.CreateUniqueJpeg(6),
            "image/jpeg",
            "receipt-6.jpg",
            ContractPaymentMethod.VodafoneCash,
            "01090000006");
        var sixthResponse = await client.PostAsync(
            $"/api/v1/payments/{bookingId}/receipt",
            sixthUpload,
            cancellationToken);

        Assert.Equal(HttpStatusCode.TooManyRequests, sixthResponse.StatusCode);
        Assert.True(sixthResponse.Headers.Contains("Retry-After"));
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

    private async Task<HttpClient> CreateVerifiedClientAsync(
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

        return client;
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
        var client = await CreateVerifiedClientAsync(email, cancellationToken);

        var response = await client.PostAsJsonAsync("/api/v1/bookings", new CreateBookingRequest(
            slotId,
            ContractDeliveryMethod.Chat,
            null), cancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<ReserveBookingResponse>(cancellationToken);
        return (client, body!.BookingId, slotId);
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
}
