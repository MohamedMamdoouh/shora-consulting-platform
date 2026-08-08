using Microsoft.Extensions.Options;
using Shora.Application.Common;
using Shora.Application.Options;
using Shora.Application.Services;

namespace Shora.Api.BackgroundJobs;

public sealed class OpsMonitoringJob(
    IServiceScopeFactory scopeFactory,
    IOptions<BackgroundJobOptions> options,
    ILogger<OpsMonitoringJob> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!options.Value.Enabled)
        {
            logger.LogInformation("Background jobs are disabled; ops monitoring will not run.");
            return;
        }

        var interval = TimeSpan.FromSeconds(options.Value.OpsMonitoringIntervalSeconds);

        await BackgroundJobHost.RunPeriodicAsync(
            scopeFactory,
            logger,
            BackgroundJobNames.OpsMonitoring,
            interval,
            async (provider, cancellationToken) =>
            {
                var monitoringService = provider.GetRequiredService<OpsMonitoringService>();
                await monitoringService.EvaluateAlertsAsync(cancellationToken);
            },
            stoppingToken);
    }
}
