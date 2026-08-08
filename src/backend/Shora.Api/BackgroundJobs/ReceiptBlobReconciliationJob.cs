using Microsoft.Extensions.Options;
using Shora.Application.Common;
using Shora.Application.Options;
using Shora.Application.Services;

namespace Shora.Api.BackgroundJobs;

public sealed class ReceiptBlobReconciliationJob(
    IServiceScopeFactory scopeFactory,
    IOptions<BackgroundJobOptions> options,
    ILogger<ReceiptBlobReconciliationJob> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!options.Value.Enabled)
        {
            logger.LogInformation("Background jobs are disabled; receipt blob reconciliation will not run.");
            return;
        }

        var interval = TimeSpan.FromSeconds(options.Value.ReceiptBlobReconciliationIntervalSeconds);

        await BackgroundJobHost.RunPeriodicAsync(
            scopeFactory,
            logger,
            BackgroundJobNames.ReceiptBlobReconciliation,
            interval,
            async (provider, cancellationToken) =>
            {
                var reconciliationService = provider.GetRequiredService<ReceiptBlobReconciliationService>();
                await reconciliationService.RunAsync(cancellationToken);
            },
            stoppingToken);
    }
}
