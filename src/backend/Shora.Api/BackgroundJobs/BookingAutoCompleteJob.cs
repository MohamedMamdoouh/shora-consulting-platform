using Microsoft.Extensions.Options;
using Shora.Application.Common;
using Shora.Application.Options;
using Shora.Application.Services;

namespace Shora.Api.BackgroundJobs;

public sealed class BookingAutoCompleteJob(
    IServiceScopeFactory scopeFactory,
    IOptions<BackgroundJobOptions> options,
    ILogger<BookingAutoCompleteJob> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!options.Value.Enabled)
        {
            logger.LogInformation("Background jobs are disabled; booking auto-complete will not run.");
            return;
        }

        var interval = TimeSpan.FromSeconds(options.Value.BookingAutoCompleteIntervalSeconds);

        await BackgroundJobHost.RunPeriodicAsync(
            scopeFactory,
            logger,
            BackgroundJobNames.BookingAutoComplete,
            interval,
            async (provider, cancellationToken) =>
            {
                var service = provider.GetRequiredService<BookingAutoCompleteService>();
                await service.RunAsync(cancellationToken);
            },
            stoppingToken);
    }
}
