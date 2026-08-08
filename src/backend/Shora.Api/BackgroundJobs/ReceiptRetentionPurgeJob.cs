using Microsoft.Extensions.Options;
using Shora.Application.Common;
using Shora.Application.Options;
using Shora.Application.Services;

namespace Shora.Api.BackgroundJobs;

public sealed class ReceiptRetentionPurgeJob(
    IServiceScopeFactory scopeFactory,
    IOptions<BackgroundJobOptions> options,
    ILogger<ReceiptRetentionPurgeJob> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!options.Value.Enabled)
        {
            logger.LogInformation("Background jobs are disabled; receipt retention purge will not run.");
            return;
        }

        var interval = TimeSpan.FromSeconds(options.Value.ReceiptRetentionPurgeIntervalSeconds);

        await BackgroundJobHost.RunPeriodicAsync(
            scopeFactory,
            logger,
            BackgroundJobNames.ReceiptRetentionPurge,
            interval,
            async (provider, cancellationToken) =>
            {
                var purgeService = provider.GetRequiredService<ReceiptRetentionPurgeService>();
                await purgeService.RunAsync(cancellationToken);
            },
            stoppingToken);
    }
}
