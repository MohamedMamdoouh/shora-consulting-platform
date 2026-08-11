using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Shora.Application.Abstractions;
using Shora.Application.Bookings;
using Shora.Application.Common;
using Shora.Application.Common.Results;
using Shora.Application.Options;
using Shora.Contracts.Booking;
using Shora.Domain.Constants;
using Shora.Domain.Entities;
using Shora.Domain.Enums;
using ContractCancellationRequestStatus = Shora.Contracts.Booking.CancellationRequestStatus;
using ContractDeliveryMethod = Shora.Contracts.Booking.DeliveryMethod;

namespace Shora.Application.Services;

public sealed class BookingService(
    IApplicationDbContext dbContext,
    IDateTimeProvider dateTimeProvider,
    BookingTransitionHelper transitionHelper,
    ICacheInvalidator cacheInvalidator,
    IFileStorage fileStorage,
    IOptions<BookingOptions> bookingOptions,
    IOptions<StorageOptions> storageOptions)
{
    public async Task<Result<ReserveBookingResponse>> ReserveAsync(
        Guid clientId,
        CreateBookingRequest request,
        CancellationToken cancellationToken = default)
    {
        var deliveryValidation = ValidateDeliveryAndPhone(request);
        if (deliveryValidation.IsFailure)
        {
            return deliveryValidation.Error!;
        }

        var normalizedPhone = deliveryValidation.Value;

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

        var emailResult = await EnsureEmailVerifiedAsync(clientId, cancellationToken);
        if (emailResult.IsFailure)
        {
            await transaction.RollbackAsync(cancellationToken);
            return emailResult.Error!;
        }

        var holdCapResult = await EnsureHoldCapAsync(clientId, cancellationToken);
        if (holdCapResult.IsFailure)
        {
            await transaction.RollbackAsync(cancellationToken);
            return holdCapResult.Error!;
        }

        var settings = await dbContext.Settings
            .AsNoTracking()
            .SingleOrDefaultAsync(s => s.Id == Settings.SingletonId, cancellationToken);

        if (settings is null)
        {
            await transaction.RollbackAsync(cancellationToken);
            return Error.NotFound(ErrorCodes.Settings.NotFound, "Settings are not configured.");
        }

        var slot = await dbContext.AvailabilitySlots
            .FromSqlInterpolated(
                $"""
                 SELECT * FROM "AvailabilitySlots" WHERE "Id" = {request.AvailabilitySlotId} FOR UPDATE
                 """)
            .FirstOrDefaultAsync(cancellationToken);

        if (slot is null || slot.IsBooked)
        {
            await transaction.RollbackAsync(cancellationToken);
            return Error.Conflict(
                ErrorCodes.Booking.SlotUnavailable,
                "The selected slot is no longer available.");
        }

        var now = dateTimeProvider.UtcNow;
        var bookingId = Guid.NewGuid();
        var receiptUploadDeadlineUtc = now.AddMinutes(settings.ReceiptUploadWindowMinutes);

        slot.IsBooked = true;
        slot.BookingId = bookingId;

        var booking = new Booking
        {
            Id = bookingId,
            ClientId = clientId,
            AvailabilitySlotId = slot.Id,
            SlotStartUtc = slot.StartTimeUtc,
            SlotEndUtc = slot.EndTimeUtc,
            DeliveryMethod = MapDeliveryMethod(request.DeliveryMethod),
            ContactPhone = normalizedPhone,
            ReceiptUploadDeadlineUtc = receiptUploadDeadlineUtc,
            CreatedAt = now
        };

        dbContext.Bookings.Add(booking);

        dbContext.Payments.Add(new Payment
        {
            Id = Guid.NewGuid(),
            BookingId = bookingId,
            Status = PaymentStatus.AwaitingReceipt,
            Amount = settings.SessionPrice,
            Currency = CurrencyCodes.Egp,
            CreatedAt = now,
            UpdatedAt = now
        });

        var auditResult = transitionHelper.RecordInitialStatus(
            booking,
            BookingStatus.PendingPayment,
            AuditActor.Client,
            clientId,
            "Booking reserved");

        if (auditResult.IsFailure)
        {
            await transaction.RollbackAsync(cancellationToken);
            return auditResult.Error!;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        await cacheInvalidator.InvalidateAvailabilityAsync(cancellationToken);

        return new ReserveBookingResponse(
            bookingId,
            new PaymentInstructionsSnapshot(
                settings.SessionPrice,
                CurrencyCodes.Egp,
                settings.VodafoneCashNumber,
                settings.InstaPayHandle,
                settings.PaymentInstructions,
                receiptUploadDeadlineUtc));
    }

    public async Task<Result> CancelHoldAsync(
        Guid clientId,
        Guid bookingId,
        CancellationToken cancellationToken = default)
    {
        var booking = await dbContext.Bookings
            .Include(b => b.Payment)
            .FirstOrDefaultAsync(b => b.Id == bookingId, cancellationToken);

        if (booking is null)
        {
            return Error.NotFound(ErrorCodes.Booking.NotFound, "Booking was not found.");
        }

        if (booking.ClientId != clientId)
        {
            return Error.Forbidden(ErrorCodes.Booking.Forbidden, "You do not have access to this booking.");
        }

        if (booking.Status is not BookingStatus.PendingPayment and not BookingStatus.PendingApproval)
        {
            return Error.Conflict(
                ErrorCodes.Booking.InvalidStatus,
                "Only unpaid holds can be cancelled.");
        }

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

        if (booking.AvailabilitySlotId is Guid slotId)
        {
            var slot = await dbContext.AvailabilitySlots
                .FromSqlInterpolated(
                    $"""
                     SELECT * FROM "AvailabilitySlots" WHERE "Id" = {slotId} FOR UPDATE
                     """)
                .FirstOrDefaultAsync(cancellationToken);

            if (slot is not null)
            {
                slot.IsBooked = false;
                slot.BookingId = null;
            }

            booking.AvailabilitySlotId = null;
        }

        var fromStatus = booking.Status;
        var now = dateTimeProvider.UtcNow;

        var transitionResult = transitionHelper.ApplyTransition(
            booking,
            BookingStatus.Cancelled,
            AuditActor.Client,
            fromStatus,
            clientId,
            "Hold cancelled by client");

        if (transitionResult.IsFailure)
        {
            await transaction.RollbackAsync(cancellationToken);
            return transitionResult;
        }

        if (booking.Payment is not null)
        {
            booking.Payment.Status = PaymentStatus.Void;
            booking.Payment.UpdatedAt = now;
        }

        EnqueueClientBookingCancelledEmail(booking, now);

        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        await cacheInvalidator.InvalidateAvailabilityAsync(cancellationToken);

        return Result.Success();
    }

    private void EnqueueClientBookingCancelledEmail(Booking booking, DateTime now)
    {
        dbContext.OutboxMessages.Add(new OutboxMessage
        {
            Id = Guid.NewGuid(),
            MessageType = OutboxMessageTypes.ClientBookingCancelledEmail,
            AggregateType = nameof(Booking),
            AggregateId = booking.Id,
            IdempotencyKey = $"{booking.Id}:{OutboxMessageTypes.ClientBookingCancelledEmail}",
            PayloadJson = JsonSerializer.Serialize(new { bookingId = booking.Id, clientId = booking.ClientId }),
            CreatedAtUtc = now,
            NextAttemptAtUtc = now,
            Status = OutboxMessageStatus.Pending
        });
    }

    public async Task<Result<MyBookingsResponse>> ListMineAsync(
        Guid clientId,
        MyBookingsQuery query,
        CancellationToken cancellationToken = default)
    {
        var validation = ValidateMyBookingsQuery(query);
        if (validation.IsFailure)
        {
            return validation.Error!;
        }

        var now = dateTimeProvider.UtcNow;
        var bookingsQuery = dbContext.Bookings
            .AsNoTracking()
            .Where(booking => booking.ClientId == clientId);

        bookingsQuery = ApplyMyBookingsStatusFilter(bookingsQuery, query.Status, now);

        var totalCount = await bookingsQuery.CountAsync(cancellationToken);
        var orderedQuery = ApplyMyBookingsOrdering(bookingsQuery, query.Status)
            .Include(booking => booking.CancellationRequest)
            .Include(booking => booking.Payment!)
            .ThenInclude(payment => payment.Receipts);

        var page = query.Page;
        var pageSize = query.PageSize;
        List<Booking> bookings;

        if (query.Status is MyBookingsStatusFilter.Upcoming or MyBookingsStatusFilter.Pending)
        {
            bookings = await orderedQuery.ToListAsync(cancellationToken);
            page = MyBookingsQueryLimits.DefaultPage;
            pageSize = totalCount;
        }
        else
        {
            bookings = await orderedQuery
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync(cancellationToken);
        }

        var settings = await dbContext.Settings
            .AsNoTracking()
            .SingleOrDefaultAsync(s => s.Id == Settings.SingletonId, cancellationToken);

        var cancelledBookingIds = bookings
            .Where(booking => booking.Status == BookingStatus.Cancelled)
            .Select(booking => booking.Id)
            .ToList();
        var cancelAudits = await LoadLatestCancelAuditsAsync(cancelledBookingIds, cancellationToken);

        var items = new List<MyBookingListItem>(bookings.Count);
        foreach (var booking in bookings)
        {
            cancelAudits.TryGetValue(booking.Id, out var cancelAudit);
            items.Add(await MapToMyBookingListItemAsync(
                booking,
                settings,
                cancelAudit,
                now,
                cancellationToken));
        }

        return new MyBookingsResponse(items, page, pageSize, totalCount);
    }

    private async Task<IReadOnlyDictionary<Guid, BookingStatusAudit>> LoadLatestCancelAuditsAsync(
        IReadOnlyCollection<Guid> bookingIds,
        CancellationToken cancellationToken)
    {
        if (bookingIds.Count == 0)
        {
            return new Dictionary<Guid, BookingStatusAudit>();
        }

        var audits = await dbContext.BookingStatusAudits
            .AsNoTracking()
            .Where(audit => bookingIds.Contains(audit.BookingId) && audit.ToStatus == BookingStatus.Cancelled)
            .OrderByDescending(audit => audit.AtUtc)
            .ToListAsync(cancellationToken);

        return audits
            .GroupBy(audit => audit.BookingId)
            .ToDictionary(group => group.Key, group => group.First());
    }

    private async Task<MyBookingListItem> MapToMyBookingListItemAsync(
        Booking booking,
        Settings? settings,
        BookingStatusAudit? cancelAudit,
        DateTime nowUtc,
        CancellationToken cancellationToken) =>
        new(
            booking.Id,
            booking.SlotStartUtc,
            booking.SlotEndUtc,
            MapDeliveryMethodToContract(booking.DeliveryMethod),
            booking.ContactPhone,
            booking.Status.ToString(),
            MyBookingLabelMapper.MapCancellationReasonLabel(cancelAudit),
            MyBookingLabelMapper.MapRefundLabel(booking.Status, booking.Payment),
            MapCancellationRequestMetadata(booking.CancellationRequest),
            MapPaymentSummary(booking, settings),
            await GetReceiptThumbnailUrlAsync(booking, cancellationToken),
            MapConsultantWhatsAppNumber(booking, settings, nowUtc));

    private static MyBookingCancellationRequestMetadata? MapCancellationRequestMetadata(
        CancellationRequest? request)
    {
        if (request is null)
        {
            return null;
        }

        return new MyBookingCancellationRequestMetadata(
            MapCancellationRequestStatus(request.Status),
            request.ReopenCount,
            request.ClientDecisionSeenAtUtc,
            request.DecisionReason,
            request.AutoDeclineAtUtc);
    }

    private static MyBookingPaymentSummary? MapPaymentSummary(Booking booking, Settings? settings)
    {
        if (settings is null
            || booking.Payment is null
            || booking.Status is not (BookingStatus.PendingPayment or BookingStatus.PendingApproval))
        {
            return null;
        }

        return new MyBookingPaymentSummary(
            booking.Payment.Amount,
            booking.Payment.Currency,
            settings.VodafoneCashNumber,
            settings.InstaPayHandle,
            settings.PaymentInstructions,
            booking.Status == BookingStatus.PendingPayment ? booking.ReceiptUploadDeadlineUtc : null,
            booking.Status == BookingStatus.PendingPayment
                ? GetLatestReceiptDeclineReason(booking.Payment)
                : null);
    }

    private static string? GetLatestReceiptDeclineReason(Payment payment)
    {
        var latestDeclinedReceipt = payment.Receipts
            .Where(receipt => receipt.ReviewStatus == ReceiptReviewStatus.Declined)
            .OrderByDescending(receipt => receipt.ReviewedAtUtc ?? receipt.UploadedAtUtc)
            .ThenByDescending(receipt => receipt.Id)
            .FirstOrDefault();

        if (latestDeclinedReceipt is null)
        {
            return null;
        }

        return !string.IsNullOrWhiteSpace(latestDeclinedReceipt.DeclineReason)
            ? latestDeclinedReceipt.DeclineReason
            : latestDeclinedReceipt.DeclineReasonCode?.ToString();
    }

    private async Task<string?> GetReceiptThumbnailUrlAsync(
        Booking booking,
        CancellationToken cancellationToken)
    {
        if (booking.Status != BookingStatus.PendingApproval || booking.Payment is null)
        {
            return null;
        }

        var receipt = booking.Payment.Receipts
            .OrderByDescending(receipt => receipt.UploadedAtUtc)
            .ThenByDescending(receipt => receipt.Id)
            .FirstOrDefault();

        if (receipt is null || !CanMintReceiptReadUrl(receipt))
        {
            return null;
        }

        var validity = TimeSpan.FromMinutes(storageOptions.Value.ReceiptReadUrlMinutes);
        return await fileStorage.GetReadUrlAsync(receipt.BlobPath, validity, cancellationToken);
    }

    private static string? MapConsultantWhatsAppNumber(
        Booking booking,
        Settings? settings,
        DateTime nowUtc)
    {
        if (settings is null
            || booking.SlotStartUtc <= nowUtc
            || booking.Status is not (BookingStatus.Confirmed or BookingStatus.CancellationRequested))
        {
            return null;
        }

        return string.IsNullOrWhiteSpace(settings.ConsultantWhatsAppNumber)
            ? null
            : settings.ConsultantWhatsAppNumber;
    }

    private static bool CanMintReceiptReadUrl(PaymentReceipt receipt) =>
        receipt.BlobState == BlobState.Finalized
        && receipt.MalwareScanStatus == MalwareScanStatus.Clean;

    private static ContractCancellationRequestStatus MapCancellationRequestStatus(
        Domain.Enums.CancellationRequestStatus status) =>
        (ContractCancellationRequestStatus)(int)status;

    private static Result ValidateMyBookingsQuery(MyBookingsQuery query)
    {
        if (query.Page < MyBookingsQueryLimits.DefaultPage)
        {
            return Error.Validation(
                ErrorCodes.General.Validation,
                "Page must be at least 1.");
        }

        if (query.PageSize is < 1 or > MyBookingsQueryLimits.MaxPageSize)
        {
            return Error.Validation(
                ErrorCodes.General.Validation,
                $"Page size must be between 1 and {MyBookingsQueryLimits.MaxPageSize}.");
        }

        return Result.Success();
    }

    private static IQueryable<Booking> ApplyMyBookingsStatusFilter(
        IQueryable<Booking> query,
        MyBookingsStatusFilter? statusFilter,
        DateTime nowUtc) =>
        statusFilter switch
        {
            MyBookingsStatusFilter.Upcoming => query.Where(booking =>
                (booking.Status == BookingStatus.Confirmed
                 || booking.Status == BookingStatus.CancellationRequested)
                && booking.SlotStartUtc > nowUtc),
            MyBookingsStatusFilter.Pending => query.Where(booking =>
                booking.Status == BookingStatus.PendingPayment
                || booking.Status == BookingStatus.PendingApproval),
            MyBookingsStatusFilter.Past => query.Where(booking =>
                booking.Status == BookingStatus.Completed
                || booking.Status == BookingStatus.Cancelled),
            _ => query
        };

    private static IOrderedQueryable<Booking> ApplyMyBookingsOrdering(
        IQueryable<Booking> query,
        MyBookingsStatusFilter? statusFilter) =>
        statusFilter switch
        {
            MyBookingsStatusFilter.Upcoming => query.OrderBy(booking => booking.SlotStartUtc),
            MyBookingsStatusFilter.Pending => query.OrderBy(booking => booking.SlotStartUtc),
            _ => query.OrderByDescending(booking => booking.SlotStartUtc)
        };

    private static ContractDeliveryMethod MapDeliveryMethodToContract(Domain.Enums.DeliveryMethod deliveryMethod) =>
        deliveryMethod switch
        {
            Domain.Enums.DeliveryMethod.VoiceCall => ContractDeliveryMethod.VoiceCall,
            Domain.Enums.DeliveryMethod.Chat => ContractDeliveryMethod.Chat,
            _ => throw new ArgumentOutOfRangeException(nameof(deliveryMethod), deliveryMethod, "Unknown delivery method.")
        };

    private static Result<string?> ValidateDeliveryAndPhone(CreateBookingRequest request)
    {
        if (request.DeliveryMethod == ContractDeliveryMethod.VoiceCall)
        {
            if (string.IsNullOrWhiteSpace(request.ContactPhone))
            {
                return Error.Validation(
                    ErrorCodes.Booking.ContactPhoneRequired,
                    "Contact phone is required for voice call delivery.");
            }

            var phoneResult = PhoneNormalizer.NormalizeToE164(request.ContactPhone);
            if (phoneResult.IsFailure)
            {
                return phoneResult.Error!;
            }

            return phoneResult.Value;
        }

        if (!string.IsNullOrWhiteSpace(request.ContactPhone))
        {
            var phoneResult = PhoneNormalizer.NormalizeToE164(request.ContactPhone);
            if (phoneResult.IsFailure)
            {
                return phoneResult.Error!;
            }

            return phoneResult.Value;
        }

        return (string?)null;
    }

    private async Task<Result> EnsureEmailVerifiedAsync(Guid clientId, CancellationToken cancellationToken)
    {
        var emailConfirmed = await dbContext.Users
            .AsNoTracking()
            .Where(user => user.Id == clientId)
            .Select(user => user.EmailConfirmed)
            .FirstOrDefaultAsync(cancellationToken);

        if (!emailConfirmed)
        {
            return Error.Forbidden(
                ErrorCodes.Booking.EmailNotVerified,
                "Verify your email before reserving a session.");
        }

        return Result.Success();
    }

    private async Task<Result> EnsureHoldCapAsync(Guid clientId, CancellationToken cancellationToken)
    {
        var pendingPayment = nameof(BookingStatus.PendingPayment);
        var pendingApproval = nameof(BookingStatus.PendingApproval);

        var holdCount = await dbContext.Bookings
            .FromSqlInterpolated(
                $"""
                 SELECT "Id", "ClientId", "AvailabilitySlotId", "SlotStartUtc", "SlotEndUtc", "DeliveryMethod", "ContactPhone", "Status", "ReceiptUploadDeadlineUtc", "CreatedAt", xmin
                 FROM "Bookings" WHERE "ClientId" = {clientId}
                 AND "Status" IN ({pendingPayment}, {pendingApproval})
                 FOR UPDATE
                 """)
            .CountAsync(cancellationToken);

        if (holdCount >= bookingOptions.Value.UnconfirmedHoldCap)
        {
            return Error.Conflict(
                ErrorCodes.Booking.HoldCapExceeded,
                "You already have the maximum number of unpaid holds.");
        }

        return Result.Success();
    }

    private static Domain.Enums.DeliveryMethod MapDeliveryMethod(ContractDeliveryMethod deliveryMethod) =>
        deliveryMethod switch
        {
            ContractDeliveryMethod.VoiceCall => Domain.Enums.DeliveryMethod.VoiceCall,
            ContractDeliveryMethod.Chat => Domain.Enums.DeliveryMethod.Chat,
            _ => throw new ArgumentOutOfRangeException(nameof(deliveryMethod), deliveryMethod, "Unknown delivery method.")
        };
}
