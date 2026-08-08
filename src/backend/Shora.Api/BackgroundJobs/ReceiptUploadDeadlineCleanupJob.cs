using Microsoft.Extensions.Options;
using Shora.Application.Common;
using Shora.Application.Options;
using Shora.Application.Services;

namespace Shora.Api.BackgroundJobs;

public sealed class ReceiptUploadDeadlineCleanupJob(
    IServiceScopeFactory scopeFactory,
    IOptions<BackgroundJobOptions> options,
    ILogger<ReceiptUploadDeadlineCleanupJob> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!options.Value.Enabled)
        {
            logger.LogInformation("Background jobs are disabled; receipt upload deadline cleanup will not run.");
            return;
        }

        var interval = TimeSpan.FromSeconds(options.Value.ReceiptUploadDeadlineCleanupIntervalSeconds);

        await BackgroundJobHost.RunPeriodicAsync(
            scopeFactory,
            logger,
            BackgroundJobNames.ReceiptUploadDeadlineCleanup,
            interval,
            async (provider, cancellationToken) =>
            {
                var cleanupService = provider.GetRequiredService<ReceiptUploadDeadlineCleanupService>();
                await cleanupService.RunAsync(cancellationToken);
            },
            stoppingToken);
    }
}
