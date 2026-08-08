using Shora.Application.Services;

namespace Shora.Api.BackgroundJobs;

internal static class BackgroundJobHost
{
    public static async Task RunPeriodicAsync(
        IServiceScopeFactory scopeFactory,
        ILogger logger,
        string jobName,
        TimeSpan interval,
        Func<IServiceProvider, CancellationToken, Task> executeAsync,
        CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                await executeAsync(scope.ServiceProvider, stoppingToken);

                var heartbeat = scope.ServiceProvider.GetRequiredService<JobHeartbeatService>();
                await heartbeat.RecordSuccessAsync(jobName, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "{JobName} failed.", jobName);

                try
                {
                    await using var scope = scopeFactory.CreateAsyncScope();
                    var heartbeat = scope.ServiceProvider.GetRequiredService<JobHeartbeatService>();
                    await heartbeat.RecordFailureAsync(jobName, exception.Message, stoppingToken);
                }
                catch (Exception heartbeatException)
                {
                    logger.LogError(
                        heartbeatException,
                        "Failed to record heartbeat failure for {JobName}.",
                        jobName);
                }
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
