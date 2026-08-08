using Microsoft.Extensions.Logging;
using Shora.Application.Abstractions;

namespace Shora.Application.Services;

public sealed class AvailabilityTopUpService(
    SlotGenerationService slotGenerationService,
    ICacheInvalidator cacheInvalidator,
    ILogger<AvailabilityTopUpService> logger)
{
    public async Task RunAsync(CancellationToken cancellationToken = default)
    {
        await slotGenerationService.GenerateHorizonAsync(cancellationToken);
        await cacheInvalidator.InvalidateAvailabilityAsync(cancellationToken);

        logger.LogInformation("Availability top-up completed.");
    }
}
