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
public class AdminEarningsEndpointTests : IDisposable
{
    private readonly AuthWebApplicationFactory _factory;

    public AdminEarningsEndpointTests(PostgresFixture sqlServer)
    {
        _factory = new AuthWebApplicationFactory(sqlServer);
    }

    [Fact]
    public async Task Get_earnings_as_admin_returns_aggregate_metrics()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var adminClient = await CreateAdminClientAsync(cancellationToken);
        var (paymentId, bookingId) = await CreateApprovedPaymentAsync("earnings-approved@example.com", cancellationToken);

        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var booking = await context.Bookings.SingleAsync(b => b.Id == bookingId, cancellationToken);
            booking.Status = BookingStatus.Cancelled;
            await context.SaveChangesAsync(cancellationToken);
        }

        var response = await adminClient.GetAsync("/api/v1/admin/earnings", cancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<AdminEarningsResponse>(cancellationToken);
        Assert.NotNull(body);

        await using var readScope = _factory.Services.CreateAsyncScope();
        var readContext = readScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var payment = await readContext.Payments.AsNoTracking().SingleAsync(p => p.Id == paymentId, cancellationToken);

        Assert.Equal(payment.Amount, body!.GrossRevenue);
        Assert.Equal(0m, body.RefundedAmount);
        Assert.Equal(payment.Amount, body.NetRevenue);
        Assert.Equal(1, body.ApprovedCount);
        Assert.Equal(0, body.RefundedCount);
        Assert.Equal(1, body.RefundDueCount);
    }

    [Fact]
    public async Task Get_earnings_reflects_recorded_refund()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var adminClient = await CreateAdminClientAsync(cancellationToken);
        var (paymentId, _) = await CreateRefundDuePaymentAsync("earnings-refunded@example.com", cancellationToken);

        var recordResponse = await adminClient.PostAsJsonAsync(
            $"/api/v1/admin/payments/{paymentId}/refunds/record",
            new RecordRefundRequest("VC-EARN-1", null),
            cancellationToken);
        Assert.Equal(HttpStatusCode.OK, recordResponse.StatusCode);

        var response = await adminClient.GetAsync("/api/v1/admin/earnings", cancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<AdminEarningsResponse>(cancellationToken);
        Assert.NotNull(body);

        await using var scope = _factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var payment = await context.Payments.AsNoTracking().SingleAsync(p => p.Id == paymentId, cancellationToken);

        Assert.Equal(payment.Amount, body!.GrossRevenue);
        Assert.Equal(payment.Amount, body.RefundedAmount);
        Assert.Equal(0m, body.NetRevenue);
        Assert.Equal(1, body.ApprovedCount);
        Assert.Equal(1, body.RefundedCount);
        Assert.Equal(0, body.RefundDueCount);
    }

    [Fact]
    public async Task Get_earnings_as_client_returns_forbidden()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var (client, _) = await CreateVerifiedClientAsync("earnings-forbidden@example.com", cancellationToken);

        var response = await client.GetAsync("/api/v1/admin/earnings", cancellationToken);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Get_earnings_rejects_invalid_date_range()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var adminClient = await CreateAdminClientAsync(cancellationToken);
        var fromUtc = DateTime.UtcNow.AddDays(5).ToString("O");
        var toUtc = DateTime.UtcNow.AddDays(1).ToString("O");

        var response = await adminClient.GetAsync(
            $"/api/v1/admin/earnings?from={Uri.EscapeDataString(fromUtc)}&to={Uri.EscapeDataString(toUtc)}",
            cancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    public void Dispose()
    {
        _factory.Dispose();
    }

    private async Task<(Guid PaymentId, Guid BookingId)> CreateApprovedPaymentAsync(
        string email,
        CancellationToken cancellationToken)
    {
        var (client, bookingId, _) = await ReserveBookingAsync(email, cancellationToken);
        await UploadReceiptAsync(client, bookingId, cancellationToken);

        var adminClient = await CreateAdminClientAsync(cancellationToken);
        var approveResponse = await adminClient.PostAsync(
            $"/api/v1/admin/bookings/{bookingId}/receipts/approve",
            null,
            cancellationToken);
        Assert.Equal(HttpStatusCode.OK, approveResponse.StatusCode);

        await using var scope = _factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var payment = await context.Payments.AsNoTracking().SingleAsync(p => p.BookingId == bookingId, cancellationToken);

        return (payment.Id, bookingId);
    }

    private async Task<(Guid PaymentId, Guid BookingId)> CreateRefundDuePaymentAsync(
        string email,
        CancellationToken cancellationToken)
    {
        var (paymentId, bookingId) = await CreateApprovedPaymentAsync(email, cancellationToken);

        await using var scope = _factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var booking = await context.Bookings.SingleAsync(b => b.Id == bookingId, cancellationToken);
        booking.Status = BookingStatus.Cancelled;

        await context.SaveChangesAsync(cancellationToken);

        return (paymentId, bookingId);
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
}
