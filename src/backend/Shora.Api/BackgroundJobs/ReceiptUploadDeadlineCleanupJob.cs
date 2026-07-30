using Microsoft.Extensions.Options;
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

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                var cleanupService = scope.ServiceProvider.GetRequiredService<ReceiptUploadDeadlineCleanupService>();
                await cleanupService.RunAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Receipt upload deadline cleanup job failed.");
            }

            try
            {
                await Task.Delay(interval, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }
    }
}
