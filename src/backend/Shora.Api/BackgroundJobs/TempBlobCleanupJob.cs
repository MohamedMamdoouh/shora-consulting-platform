using Microsoft.Extensions.Options;
using Shora.Application.Common;
using Shora.Application.Options;
using Shora.Application.Services;

namespace Shora.Api.BackgroundJobs;

public sealed class TempBlobCleanupJob(
    IServiceScopeFactory scopeFactory,
    IOptions<BackgroundJobOptions> options,
    ILogger<TempBlobCleanupJob> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!options.Value.Enabled)
        {
            logger.LogInformation("Background jobs are disabled; temp blob cleanup will not run.");
            return;
        }

        var interval = TimeSpan.FromSeconds(options.Value.TempBlobCleanupIntervalSeconds);

        await BackgroundJobHost.RunPeriodicAsync(
            scopeFactory,
            logger,
            BackgroundJobNames.TempBlobCleanup,
            interval,
            async (provider, cancellationToken) =>
            {
                var cleanupService = provider.GetRequiredService<TempBlobCleanupService>();
                await cleanupService.RunAsync(cancellationToken);
            },
            stoppingToken);
    }
}
