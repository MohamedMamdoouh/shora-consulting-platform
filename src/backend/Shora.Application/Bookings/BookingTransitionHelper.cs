using Shora.Application.Abstractions;
using Shora.Application.Common;
using Shora.Application.Common.Results;
using Shora.Domain.Entities;
using Shora.Domain.Enums;

namespace Shora.Application.Bookings;

public sealed class BookingTransitionHelper(
    IApplicationDbContext dbContext,
    IDateTimeProvider dateTimeProvider)
{
    public Result ApplyTransition(
        Booking booking,
        BookingStatus toStatus,
        AuditActor actor,
        BookingStatus? expectedFromStatus = null,
        Guid? actorUserId = null,
        string? reason = null)
    {
        if (expectedFromStatus.HasValue && booking.Status != expectedFromStatus.Value)
        {
            return Result.Failure(
                Error.Conflict(
                    ErrorCodes.Booking.InvalidStatus,
                    $"Booking status must be {expectedFromStatus.Value} to apply this transition."));
        }

        var fromStatus = expectedFromStatus ?? booking.Status;

        booking.Status = toStatus;

        dbContext.BookingStatusAudits.Add(new BookingStatusAudit
        {
            Id = Guid.NewGuid(),
            BookingId = booking.Id,
            FromStatus = fromStatus,
            ToStatus = toStatus,
            Actor = actor,
            ActorUserId = actorUserId,
            Reason = reason,
            AtUtc = dateTimeProvider.UtcNow
        });

        return Result.Success();
    }

    public Result RecordInitialStatus(
        Booking booking,
        BookingStatus initialStatus,
        AuditActor actor,
        Guid? actorUserId = null,
        string? reason = null)
    {
        booking.Status = initialStatus;

        dbContext.BookingStatusAudits.Add(new BookingStatusAudit
        {
            Id = Guid.NewGuid(),
            BookingId = booking.Id,
            FromStatus = null,
            ToStatus = initialStatus,
            Actor = actor,
            ActorUserId = actorUserId,
            Reason = reason,
            AtUtc = dateTimeProvider.UtcNow
        });

        return Result.Success();
    }
}
