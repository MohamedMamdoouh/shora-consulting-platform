using Microsoft.Extensions.Options;
using Shora.Application.Common;
using Shora.Application.Options;
using Shora.Application.Services;

namespace Shora.Api.BackgroundJobs;

public sealed class AvailabilityTopUpJob(
    IServiceScopeFactory scopeFactory,
    IOptions<BackgroundJobOptions> options,
    ILogger<AvailabilityTopUpJob> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!options.Value.Enabled)
        {
            logger.LogInformation("Background jobs are disabled; availability top-up will not run.");
            return;
        }

        var interval = TimeSpan.FromSeconds(options.Value.AvailabilityTopUpIntervalSeconds);

        await BackgroundJobHost.RunPeriodicAsync(
            scopeFactory,
            logger,
            BackgroundJobNames.AvailabilityTopUp,
            interval,
            async (provider, cancellationToken) =>
            {
                var topUpService = provider.GetRequiredService<AvailabilityTopUpService>();
                await topUpService.RunAsync(cancellationToken);
            },
            stoppingToken);
    }
}
