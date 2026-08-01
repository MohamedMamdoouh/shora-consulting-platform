using Microsoft.EntityFrameworkCore;
using Shora.Application.Payments;
using Shora.Domain.Entities;
using Shora.Domain.Enums;
using Shora.Infrastructure.Data;

namespace Shora.Tests.Unit.Payments;

public class ReceiptAntiReplayCheckerTests
{
    [Fact]
    public async Task DetectWarningsAsync_flags_duplicate_hash_from_other_booking()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var context = CreateContext();
        var checker = new ReceiptAntiReplayChecker(context);

        var existingBookingId = Guid.NewGuid();
        var existingPaymentId = Guid.NewGuid();
        const string sharedHash = "abc123";

        context.Bookings.Add(new Booking { Id = existingBookingId, ClientId = Guid.NewGuid(), Status = BookingStatus.PendingApproval, RowVersion = [] });
        context.Payments.Add(new Payment
        {
            Id = existingPaymentId,
            BookingId = existingBookingId,
            Status = PaymentStatus.UnderReview,
            Amount = 500m,
            Method = PaymentMethod.VodafoneCash,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });
        context.PaymentReceipts.Add(new PaymentReceipt
        {
            Id = Guid.NewGuid(),
            PaymentId = existingPaymentId,
            ContentHashSha256 = sharedHash,
            BlobPath = "receipts/test",
            OriginalFileName = "a.jpg",
            ContentType = "image/jpeg",
            SizeBytes = 1,
            UploadedAtUtc = DateTime.UtcNow,
            BlobState = BlobState.Finalized,
            MalwareScanStatus = MalwareScanStatus.Pending,
            ReviewStatus = ReceiptReviewStatus.Pending
        });
        await context.SaveChangesAsync(cancellationToken);

        var warnings = await checker.DetectWarningsAsync(
            sharedHash,
            Guid.NewGuid(),
            cancellationToken);

        Assert.Equal(ReceiptReviewWarning.DuplicateContentHash, warnings);
    }

    [Fact]
    public async Task DetectWarningsAsync_returns_none_when_hash_is_unique()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var context = CreateContext();
        var checker = new ReceiptAntiReplayChecker(context);

        var warnings = await checker.DetectWarningsAsync(
            "unique-hash",
            Guid.NewGuid(),
            cancellationToken);

        Assert.Equal(ReceiptReviewWarning.None, warnings);
    }

    private static ApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new ApplicationDbContext(options);
    }
}
