using Microsoft.EntityFrameworkCore;
using Shora.Application.Abstractions;
using Shora.Application.Availability;
using Shora.Application.Common;
using Shora.Application.Common.Results;
using Shora.Contracts.Availability;
using Shora.Domain.Entities;

namespace Shora.Application.Services;

public sealed class AdminBlockedDateService(
    IApplicationDbContext dbContext,
    SlotGenerationService slotGenerationService,
    ICacheInvalidator cacheInvalidator)
{
    public async Task<Result<IReadOnlyList<BlockedDateResponse>>> ListAsync(
        CancellationToken cancellationToken = default)
    {
        var blockedDates = await dbContext.BlockedDates
            .AsNoTracking()
            .OrderBy(block => block.StartUtc)
            .ThenBy(block => block.EndUtc)
            .Select(block => new BlockedDateResponse(block.Id, block.StartUtc, block.EndUtc, block.Reason))
            .ToListAsync(cancellationToken);

        return blockedDates;
    }

    public async Task<Result<BlockedDateResponse>> CreateAsync(
        ValidatedBlockedDate validated,
        CancellationToken cancellationToken = default)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

        var overlappingSlots = await dbContext.AvailabilitySlots
            .FromSqlInterpolated(
                $"""
                 SELECT * FROM "AvailabilitySlots"
                 WHERE "StartTimeUtc" < {validated.EndUtc} AND "EndTimeUtc" > {validated.StartUtc}
                 FOR UPDATE
                 """)
            .ToListAsync(cancellationToken);

        var bookedBookingIds = overlappingSlots
            .Where(slot => slot.IsBooked && slot.BookingId.HasValue)
            .Select(slot => slot.BookingId!.Value)
            .Distinct()
            .ToList();

        if (bookedBookingIds.Count > 0)
        {
            var conflictingBookingIds = await dbContext.Bookings
                .Where(booking => bookedBookingIds.Contains(booking.Id)
                    && BlockedDateConflictPolicy.ActiveBlockingStatuses.Contains(booking.Status))
                .Select(booking => booking.Id)
                .ToListAsync(cancellationToken);

            if (conflictingBookingIds.Count > 0)
            {
                await transaction.RollbackAsync(cancellationToken);

                return Error.Conflict(
                    ErrorCodes.Availability.BlockedRangeConflictsWithBookings,
                    "The blocked range overlaps one or more reserved sessions. Cancel those bookings first.",
                    new Dictionary<string, object?>
                    {
                        ["conflictingBookingIds"] = conflictingBookingIds
                    });
            }
        }

        foreach (var slot in overlappingSlots.Where(slot => !slot.IsBooked))
        {
            dbContext.AvailabilitySlots.Remove(slot);
        }

        var blockedDate = new BlockedDate
        {
            Id = Guid.NewGuid(),
            StartUtc = validated.StartUtc,
            EndUtc = validated.EndUtc,
            Reason = validated.Reason
        };

        dbContext.BlockedDates.Add(blockedDate);
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        await cacheInvalidator.InvalidateAvailabilityAsync(cancellationToken);

        return MapResponse(blockedDate);
    }

    public async Task<Result> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var blockedDate = await dbContext.BlockedDates
            .FirstOrDefaultAsync(block => block.Id == id, cancellationToken);

        if (blockedDate is null)
        {
            return Error.NotFound(
                ErrorCodes.Availability.BlockedDateNotFound,
                "Blocked date was not found.");
        }

        dbContext.BlockedDates.Remove(blockedDate);
        await dbContext.SaveChangesAsync(cancellationToken);
        await slotGenerationService.GenerateHorizonAsync(cancellationToken);
        await cacheInvalidator.InvalidateAvailabilityAsync(cancellationToken);

        return Result.Success();
    }

    private static BlockedDateResponse MapResponse(BlockedDate blockedDate) =>
        new(blockedDate.Id, blockedDate.StartUtc, blockedDate.EndUtc, blockedDate.Reason);
}
