using Microsoft.EntityFrameworkCore;
using Shora.Application.Abstractions;
using Shora.Application.Availability;
using Shora.Application.Common;
using Shora.Application.Common.Results;
using Shora.Contracts.Availability;
using Shora.Domain.Entities;

namespace Shora.Application.Services;

public sealed class AdminAvailabilityService(
    IApplicationDbContext dbContext,
    SlotGenerationService slotGenerationService,
    ICacheInvalidator cacheInvalidator)
{
    public async Task<Result<IReadOnlyList<AvailabilityWindowResponse>>> ListWindowsAsync(
        CancellationToken cancellationToken = default)
    {
        var windows = await dbContext.AvailabilityWindows
            .AsNoTracking()
            .OrderBy(window => window.DayOfWeek)
            .ThenBy(window => window.StartTime)
            .Select(window => new AvailabilityWindowResponse(
                window.Id,
                window.DayOfWeek,
                window.StartTime,
                window.EndTime,
                window.IsActive))
            .ToListAsync(cancellationToken);

        return windows;
    }

    public async Task<Result<AvailabilityWindowResponse>> CreateWindowAsync(
        ValidatedAvailabilityWindow validated,
        CancellationToken cancellationToken = default)
    {
        var window = new AvailabilityWindow
        {
            Id = Guid.NewGuid(),
            DayOfWeek = validated.DayOfWeek,
            StartTime = validated.StartTime,
            EndTime = validated.EndTime,
            IsActive = validated.IsActive
        };

        dbContext.AvailabilityWindows.Add(window);
        await PersistAndRegenerateAsync(cancellationToken);

        return MapResponse(window);
    }

    public async Task<Result<AvailabilityWindowResponse>> UpdateWindowAsync(
        Guid id,
        ValidatedAvailabilityWindow validated,
        CancellationToken cancellationToken = default)
    {
        var window = await dbContext.AvailabilityWindows
            .FirstOrDefaultAsync(w => w.Id == id, cancellationToken);

        if (window is null)
        {
            return Error.NotFound(
                ErrorCodes.Availability.WindowNotFound,
                "Availability window was not found.");
        }

        window.DayOfWeek = validated.DayOfWeek;
        window.StartTime = validated.StartTime;
        window.EndTime = validated.EndTime;
        window.IsActive = validated.IsActive;

        await PersistAndRegenerateAsync(cancellationToken);

        return MapResponse(window);
    }

    public async Task<Result> DeleteWindowAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var window = await dbContext.AvailabilityWindows
            .FirstOrDefaultAsync(w => w.Id == id, cancellationToken);

        if (window is null)
        {
            return Error.NotFound(
                ErrorCodes.Availability.WindowNotFound,
                "Availability window was not found.");
        }

        dbContext.AvailabilityWindows.Remove(window);
        await PersistAndRegenerateAsync(cancellationToken);

        return Result.Success();
    }

    private async Task PersistAndRegenerateAsync(CancellationToken cancellationToken)
    {
        await dbContext.SaveChangesAsync(cancellationToken);
        await slotGenerationService.GenerateHorizonAsync(cancellationToken);
        await cacheInvalidator.InvalidateAvailabilityAsync(cancellationToken);
    }

    private static AvailabilityWindowResponse MapResponse(AvailabilityWindow window) =>
        new(window.Id, window.DayOfWeek, window.StartTime, window.EndTime, window.IsActive);
}
