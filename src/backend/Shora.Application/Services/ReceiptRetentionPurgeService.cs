using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Shora.Application.Abstractions;
using Shora.Domain.Entities;
using Shora.Domain.Enums;

namespace Shora.Application.Services;

public sealed class ReceiptRetentionPurgeService(
    IApplicationDbContext dbContext,
    IFileStorage fileStorage,
    IDateTimeProvider dateTimeProvider,
    ILogger<ReceiptRetentionPurgeService> logger)
{
    public async Task<int> RunAsync(CancellationToken cancellationToken = default)
    {
        var settings = await dbContext.Settings
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == Settings.SingletonId, cancellationToken);

        if (settings is null)
        {
            logger.LogWarning("Receipt retention purge skipped because settings were not found.");
            return 0;
        }

        var now = dateTimeProvider.UtcNow;
        var cutoff = now.AddMonths(-settings.ReceiptRetentionMonths);

        var candidateIds = await dbContext.PaymentReceipts
            .AsNoTracking()
            .Where(receipt =>
                receipt.UploadedAtUtc <= cutoff
                && (receipt.BlobState == BlobState.Finalized
                    || receipt.BlobState == BlobState.BlobFinalizePending))
            .Select(receipt => receipt.Id)
            .ToListAsync(cancellationToken);

        if (candidateIds.Count == 0)
        {
            return 0;
        }

        var processedCount = 0;

        foreach (var receiptId in candidateIds)
        {
            if (await TryPurgeReceiptAsync(receiptId, cutoff, cancellationToken))
            {
                processedCount++;
            }
        }

        if (processedCount > 0)
        {
            logger.LogInformation(
                "Receipt retention purge processed {ProcessedCount} of {CandidateCount} expired receipts.",
                processedCount,
                candidateIds.Count);
        }

        return processedCount;
    }

    private async Task<bool> TryPurgeReceiptAsync(
        Guid receiptId,
        DateTime cutoff,
        CancellationToken cancellationToken)
    {
        var receipt = await dbContext.PaymentReceipts
            .FirstOrDefaultAsync(r => r.Id == receiptId, cancellationToken);

        if (receipt is null
            || receipt.UploadedAtUtc > cutoff
            || (receipt.BlobState != BlobState.Finalized
                && receipt.BlobState != BlobState.BlobFinalizePending))
        {
            return false;
        }

        try
        {
            await fileStorage.DeleteAsync(receipt.BlobPath, cancellationToken);
        }
        catch (Exception exception)
        {
            logger.LogWarning(
                exception,
                "Failed to delete receipt blob {BlobPath} for receipt {ReceiptId}; continuing with metadata scrub.",
                receipt.BlobPath,
                receipt.Id);
        }

        receipt.OriginalFileName = "[purged]";
        receipt.ContentType = string.Empty;
        receipt.ContentHashSha256 = string.Empty;
        receipt.SizeBytes = 0;
        receipt.SenderReference = null;
        receipt.BlobState = BlobState.Missing;

        await dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }
}
