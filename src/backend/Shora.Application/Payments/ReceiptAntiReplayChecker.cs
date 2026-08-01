using Microsoft.EntityFrameworkCore;
using Shora.Application.Abstractions;
using Shora.Domain.Enums;

namespace Shora.Application.Payments;

public sealed class ReceiptAntiReplayChecker(IApplicationDbContext dbContext)
{
    public async Task<ReceiptReviewWarning> DetectWarningsAsync(
        string contentHashSha256,
        Guid bookingId,
        CancellationToken cancellationToken = default)
    {
        var hasDuplicateHash = await dbContext.PaymentReceipts
            .AsNoTracking()
            .AnyAsync(
                r => r.ContentHashSha256 == contentHashSha256
                     && r.Payment.BookingId != bookingId,
                cancellationToken);

        return hasDuplicateHash
            ? ReceiptReviewWarning.DuplicateContentHash
            : ReceiptReviewWarning.None;
    }
}
