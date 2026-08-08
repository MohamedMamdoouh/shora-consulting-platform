using Microsoft.Extensions.Options;
using Shora.Application.Common;
using Shora.Application.Options;
using Shora.Application.Services;

namespace Shora.Api.BackgroundJobs;

public sealed class OutboxDispatcherJob(
    IServiceScopeFactory scopeFactory,
    IOptions<BackgroundJobOptions> options,
    ILogger<OutboxDispatcherJob> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!options.Value.Enabled)
        {
            logger.LogInformation("Background jobs are disabled; outbox dispatcher will not run.");
            return;
        }

        var interval = TimeSpan.FromSeconds(options.Value.OutboxDispatcherIntervalSeconds);

        await BackgroundJobHost.RunPeriodicAsync(
            scopeFactory,
            logger,
            BackgroundJobNames.OutboxDispatcher,
            interval,
            async (provider, cancellationToken) =>
            {
                var dispatcher = provider.GetRequiredService<OutboxDispatcherService>();
                await dispatcher.RunAsync(cancellationToken);
            },
            stoppingToken);
    }
}
