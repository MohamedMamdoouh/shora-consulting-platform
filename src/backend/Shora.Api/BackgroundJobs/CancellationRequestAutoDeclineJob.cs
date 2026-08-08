using Microsoft.Extensions.Options;
using Shora.Application.Common;
using Shora.Application.Options;
using Shora.Application.Services;

namespace Shora.Api.BackgroundJobs;

public sealed class CancellationRequestAutoDeclineJob(
    IServiceScopeFactory scopeFactory,
    IOptions<BackgroundJobOptions> options,
    ILogger<CancellationRequestAutoDeclineJob> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!options.Value.Enabled)
        {
            logger.LogInformation(
                "Background jobs are disabled; cancellation request auto-decline will not run.");
            return;
        }

        var interval = TimeSpan.FromSeconds(options.Value.CancellationRequestAutoDeclineIntervalSeconds);

        await BackgroundJobHost.RunPeriodicAsync(
            scopeFactory,
            logger,
            BackgroundJobNames.CancellationRequestAutoDecline,
            interval,
            async (provider, cancellationToken) =>
            {
                var service = provider.GetRequiredService<CancellationRequestAutoDeclineService>();
                await service.RunAsync(cancellationToken);
            },
            stoppingToken);
    }
}
