using Microsoft.Extensions.Options;
using Shora.Application.Common;
using Shora.Application.Options;
using Shora.Application.Services;

namespace Shora.Api.BackgroundJobs;

public sealed class RefreshTokenPurgeJob(
    IServiceScopeFactory scopeFactory,
    IOptions<BackgroundJobOptions> options,
    ILogger<RefreshTokenPurgeJob> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!options.Value.Enabled)
        {
            logger.LogInformation("Background jobs are disabled; refresh token purge will not run.");
            return;
        }

        var interval = TimeSpan.FromSeconds(options.Value.RefreshTokenPurgeIntervalSeconds);

        await BackgroundJobHost.RunPeriodicAsync(
            scopeFactory,
            logger,
            BackgroundJobNames.RefreshTokenPurge,
            interval,
            async (provider, cancellationToken) =>
            {
                var purgeService = provider.GetRequiredService<RefreshTokenPurgeService>();
                await purgeService.RunAsync(cancellationToken);
            },
            stoppingToken);
    }
}
