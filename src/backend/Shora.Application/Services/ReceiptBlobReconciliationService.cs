using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Shora.Application.Abstractions;
using Shora.Application.Options;
using Shora.Domain.Enums;

namespace Shora.Application.Services;

public sealed class ReceiptBlobReconciliationService(
    IApplicationDbContext dbContext,
    IFileStorage fileStorage,
    IOptions<BackgroundJobOptions> backgroundJobOptions,
    ILogger<ReceiptBlobReconciliationService> logger)
{
    private const string TempPrefix = "temp/";

    public async Task<int> RunAsync(CancellationToken cancellationToken = default)
    {
        var repairedCount = await RepairFinalizePendingReceiptsAsync(cancellationToken);

        var tempDeletedCount = await fileStorage.DeleteBlobsWithPrefixOlderThanAsync(
            TempPrefix,
            TimeSpan.FromHours(backgroundJobOptions.Value.ReconciliationTempBlobMaxAgeHours),
            cancellationToken);

        if (tempDeletedCount > 0)
        {
            logger.LogInformation(
                "Receipt blob reconciliation deleted {DeletedCount} orphan temp blob(s).",
                tempDeletedCount);
        }

        return repairedCount + tempDeletedCount;
    }

    private async Task<int> RepairFinalizePendingReceiptsAsync(CancellationToken cancellationToken)
    {
        var receiptIds = await dbContext.PaymentReceipts
            .AsNoTracking()
            .Where(receipt => receipt.BlobState == BlobState.BlobFinalizePending)
            .Select(receipt => receipt.Id)
            .ToListAsync(cancellationToken);

        if (receiptIds.Count == 0)
        {
            return 0;
        }

        var repairedCount = 0;

        foreach (var receiptId in receiptIds)
        {
            if (await TryRepairReceiptAsync(receiptId, cancellationToken))
            {
                repairedCount++;
            }
        }

        if (repairedCount > 0)
        {
            logger.LogInformation(
                "Receipt blob reconciliation repaired {RepairedCount} of {CandidateCount} pending receipt(s).",
                repairedCount,
                receiptIds.Count);
        }

        return repairedCount;
    }

    private async Task<bool> TryRepairReceiptAsync(Guid receiptId, CancellationToken cancellationToken)
    {
        var receipt = await dbContext.PaymentReceipts
            .FirstOrDefaultAsync(r => r.Id == receiptId, cancellationToken);

        if (receipt is null || receipt.BlobState != BlobState.BlobFinalizePending)
        {
            return false;
        }

        if (await fileStorage.ExistsAsync(receipt.BlobPath, cancellationToken))
        {
            receipt.BlobState = BlobState.Finalized;
            await dbContext.SaveChangesAsync(cancellationToken);

            logger.LogInformation(
                "Receipt blob reconciliation finalized receipt {ReceiptId} at {BlobPath}.",
                receipt.Id,
                receipt.BlobPath);

            return true;
        }

        receipt.BlobState = BlobState.Missing;
        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogWarning(
            "Receipt blob reconciliation marked receipt {ReceiptId} missing; blob not found at {BlobPath}.",
            receipt.Id,
            receipt.BlobPath);

        return true;
    }
}
