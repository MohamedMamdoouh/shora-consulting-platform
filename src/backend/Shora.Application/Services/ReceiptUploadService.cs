using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Shora.Application.Abstractions;
using Shora.Application.Bookings;
using Shora.Application.Common;
using Shora.Application.Common.Results;
using Shora.Application.Options;
using Shora.Application.Payments;
using Shora.Contracts.Payments;
using Shora.Domain.Entities;
using Shora.Domain.Enums;
using ContractPaymentMethod = Shora.Contracts.Payments.PaymentMethod;

namespace Shora.Application.Services;

public sealed class ReceiptUploadService(
    IApplicationDbContext dbContext,
    IFileStorage fileStorage,
    IMalwareScanner malwareScanner,
    IDateTimeProvider dateTimeProvider,
    BookingTransitionHelper transitionHelper,
    ReceiptAntiReplayChecker antiReplayChecker,
    IOptions<ReceiptUploadOptions> receiptUploadOptions,
    ILogger<ReceiptUploadService> logger)
{
    public async Task<Result<UploadReceiptResponse>> UploadAsync(
        Guid clientId,
        Guid bookingId,
        Stream content,
        string contentType,
        string originalFileName,
        long declaredSizeBytes,
        ContractPaymentMethod method,
        string? senderReference,
        CancellationToken cancellationToken = default)
    {
        using var _ = PaymentLogScope.Begin(logger, bookingId);
        ArgumentNullException.ThrowIfNull(content);

        var maxSizeBytes = receiptUploadOptions.Value.MaxSizeBytes;
        if (declaredSizeBytes > maxSizeBytes)
        {
            return Error.PayloadTooLarge(
                ErrorCodes.Payment.ReceiptTooLarge,
                $"Receipt file must be {maxSizeBytes / (1024 * 1024)} MB or smaller.");
        }

        var fileBytes = await ReadBoundedAsync(content, maxSizeBytes, cancellationToken);
        if (fileBytes.Length > maxSizeBytes)
        {
            return Error.PayloadTooLarge(
                ErrorCodes.Payment.ReceiptTooLarge,
                $"Receipt file must be {maxSizeBytes / (1024 * 1024)} MB or smaller.");
        }

        var validationResult = ReceiptContentValidator.Validate(fileBytes, contentType);
        if (validationResult.IsFailure)
        {
            return validationResult.Error!;
        }

        var normalizedContentType = contentType.Trim().ToLowerInvariant();
        var contentHash = Convert.ToHexString(SHA256.HashData(fileBytes)).ToLowerInvariant();
        var domainMethod = MapPaymentMethod(method);

        string? tempPath = null;

        try
        {
            await using var uploadStream = new MemoryStream(fileBytes, writable: false);
            tempPath = await fileStorage.UploadTempAsync(uploadStream, normalizedContentType, cancellationToken);

            await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

            var booking = await dbContext.Bookings
                .FromSqlInterpolated(
                    $"""
                     SELECT * FROM Bookings WITH (UPDLOCK, ROWLOCK)
                     WHERE Id = {bookingId}
                     """)
                .FirstOrDefaultAsync(cancellationToken);

            if (booking is null)
            {
                await transaction.RollbackAsync(cancellationToken);
                return Error.NotFound(ErrorCodes.Booking.NotFound, "Booking was not found.");
            }

            if (booking.ClientId != clientId)
            {
                await transaction.RollbackAsync(cancellationToken);
                return Error.Forbidden(ErrorCodes.Booking.Forbidden, "You do not have access to this booking.");
            }

            if (booking.Status != BookingStatus.PendingPayment)
            {
                await transaction.RollbackAsync(cancellationToken);
                return Error.Conflict(
                    ErrorCodes.Payment.InvalidStatus,
                    "Receipt upload is only allowed while the booking is awaiting payment.");
            }

            var now = dateTimeProvider.UtcNow;
            if (booking.ReceiptUploadDeadlineUtc is not { } deadline || deadline <= now)
            {
                await transaction.RollbackAsync(cancellationToken);
                return Error.Conflict(
                    ErrorCodes.Payment.UploadDeadlinePassed,
                    "The receipt upload deadline has passed.");
            }

            var payment = await dbContext.Payments
                .FirstOrDefaultAsync(p => p.BookingId == bookingId, cancellationToken);

            if (payment is null)
            {
                await transaction.RollbackAsync(cancellationToken);
                return Error.NotFound(ErrorCodes.Payment.NotFound, "Payment was not found.");
            }

            if (payment.Status != PaymentStatus.AwaitingReceipt)
            {
                await transaction.RollbackAsync(cancellationToken);
                return Error.Conflict(
                    ErrorCodes.Payment.InvalidStatus,
                    "A receipt has already been submitted for this booking.");
            }

            var receiptId = Guid.NewGuid();
            var finalBlobPath = $"receipts/{payment.Id}/{receiptId}";
            var normalizedSenderReference = NormalizeSenderReference(senderReference);
            var reviewWarnings = await antiReplayChecker.DetectWarningsAsync(
                contentHash,
                bookingId,
                cancellationToken);

            var receipt = new PaymentReceipt
            {
                Id = receiptId,
                PaymentId = payment.Id,
                BlobPath = finalBlobPath,
                OriginalFileName = SanitizeFileName(originalFileName),
                ContentType = normalizedContentType,
                ContentHashSha256 = contentHash,
                SizeBytes = fileBytes.Length,
                SenderReference = normalizedSenderReference,
                UploadedAtUtc = now,
                BlobState = BlobState.TempUploaded,
                MalwareScanStatus = MalwareScanStatus.Pending,
                ReviewStatus = ReceiptReviewStatus.Pending,
                ReviewWarnings = reviewWarnings
            };

            dbContext.PaymentReceipts.Add(receipt);

            payment.Method = domainMethod;
            payment.Status = PaymentStatus.UnderReview;
            payment.UpdatedAt = now;

            var transitionResult = transitionHelper.ApplyTransition(
                booking,
                BookingStatus.PendingApproval,
                AuditActor.Client,
                BookingStatus.PendingPayment,
                clientId,
                "Receipt uploaded");

            if (transitionResult.IsFailure)
            {
                await transaction.RollbackAsync(cancellationToken);
                return transitionResult.Error!;
            }

            booking.ReceiptUploadDeadlineUtc = null;

            EnqueueAdminReceiptUploadedEmail(booking, payment, receipt, now);

            try
            {
                await dbContext.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
            }
            catch (DbUpdateConcurrencyException)
            {
                await transaction.RollbackAsync(cancellationToken);
                return Error.Conflict(
                    ErrorCodes.Payment.InvalidStatus,
                    "The booking was updated by another action. Please refresh and try again.");
            }

            try
            {
                await fileStorage.FinalizeAsync(tempPath, finalBlobPath, cancellationToken);
                tempPath = null;

                receipt.BlobState = BlobState.Finalized;
                receipt.MalwareScanStatus = await ScanReceiptAsync(fileBytes, normalizedContentType, cancellationToken);
                await dbContext.SaveChangesAsync(cancellationToken);
            }
            catch (Exception)
            {
                receipt.BlobState = BlobState.BlobFinalizePending;
                await dbContext.SaveChangesAsync(cancellationToken);
            }

            logger.LogInformation(
                "Receipt uploaded for booking {BookingId} payment {PaymentId} receipt {ReceiptId}",
                booking.Id,
                payment.Id,
                receipt.Id);

            return new UploadReceiptResponse(
                receipt.Id,
                booking.Id,
                nameof(BookingStatus.PendingApproval),
                ReceiptReviewWarningMapper.ToWarningCodes(reviewWarnings));
        }
        finally
        {
            if (tempPath is not null)
            {
                try
                {
                    await fileStorage.DeleteAsync(tempPath, cancellationToken);
                }
                catch (Exception)
                {
                    // Best-effort cleanup for abandoned temp blobs.
                }
            }
        }
    }

    private async Task<MalwareScanStatus> ScanReceiptAsync(
        byte[] fileBytes,
        string contentType,
        CancellationToken cancellationToken)
    {
        try
        {
            return await malwareScanner.ScanAsync(fileBytes, contentType, cancellationToken);
        }
        catch (Exception)
        {
            return MalwareScanStatus.Suspicious;
        }
    }

    private void EnqueueAdminReceiptUploadedEmail(
        Booking booking,
        Payment payment,
        PaymentReceipt receipt,
        DateTime now)
    {
        dbContext.OutboxMessages.Add(new OutboxMessage
        {
            Id = Guid.NewGuid(),
            MessageType = OutboxMessageTypes.AdminReceiptUploadedEmail,
            AggregateType = nameof(Booking),
            AggregateId = booking.Id,
            IdempotencyKey = $"{booking.Id}:{OutboxMessageTypes.AdminReceiptUploadedEmail}:{receipt.Id}",
            PayloadJson = JsonSerializer.Serialize(new
            {
                bookingId = booking.Id,
                paymentId = payment.Id,
                receiptId = receipt.Id,
                clientId = booking.ClientId
            }),
            CreatedAtUtc = now,
            NextAttemptAtUtc = now,
            Status = OutboxMessageStatus.Pending
        });
    }

    private static async Task<byte[]> ReadBoundedAsync(
        Stream content,
        int maxSizeBytes,
        CancellationToken cancellationToken)
    {
        if (content is MemoryStream memoryStream && memoryStream.TryGetBuffer(out var buffer))
        {
            var length = (int)Math.Min(memoryStream.Length, maxSizeBytes + 1);
            var bytes = new byte[length];
            Array.Copy(buffer.Array!, buffer.Offset, bytes, 0, length);
            return bytes;
        }

        using var boundedStream = new MemoryStream();
        var chunk = new byte[81920];
        var totalRead = 0;

        while (true)
        {
            var read = await content.ReadAsync(chunk.AsMemory(0, chunk.Length), cancellationToken);
            if (read == 0)
            {
                break;
            }

            totalRead += read;
            if (totalRead > maxSizeBytes)
            {
                return boundedStream.ToArray();
            }

            boundedStream.Write(chunk, 0, read);
        }

        return boundedStream.ToArray();
    }

    private static Domain.Enums.PaymentMethod MapPaymentMethod(ContractPaymentMethod method) =>
        method switch
        {
            ContractPaymentMethod.VodafoneCash => Domain.Enums.PaymentMethod.VodafoneCash,
            ContractPaymentMethod.InstaPay => Domain.Enums.PaymentMethod.InstaPay,
            _ => throw new ArgumentOutOfRangeException(nameof(method), method, "Unknown payment method.")
        };

    private static string SanitizeFileName(string originalFileName)
    {
        if (string.IsNullOrWhiteSpace(originalFileName))
        {
            return "receipt";
        }

        var fileName = Path.GetFileName(originalFileName.Trim());
        return string.IsNullOrWhiteSpace(fileName) ? "receipt" : fileName[..Math.Min(fileName.Length, 260)];
    }

    private static string? NormalizeSenderReference(string? senderReference)
    {
        if (string.IsNullOrWhiteSpace(senderReference))
        {
            return null;
        }

        var trimmed = senderReference.Trim();
        return trimmed.Length <= 200 ? trimmed : trimmed[..200];
    }
}
