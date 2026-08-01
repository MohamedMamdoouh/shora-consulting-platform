using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Shora.Application.Abstractions;
using Shora.Application.Options;

namespace Shora.Application.Services;

public sealed class TempBlobCleanupService(
    IFileStorage fileStorage,
    IOptions<BackgroundJobOptions> options,
    ILogger<TempBlobCleanupService> logger)
{
    private const string TempPrefix = "temp/";

    public async Task<int> RunAsync(CancellationToken cancellationToken = default)
    {
        var maxAge = TimeSpan.FromHours(options.Value.TempBlobMaxAgeHours);
        var deletedCount = await fileStorage.DeleteBlobsWithPrefixOlderThanAsync(
            TempPrefix,
            maxAge,
            cancellationToken);

        if (deletedCount > 0)
        {
            logger.LogInformation(
                "Temp blob cleanup deleted {DeletedCount} blob(s) older than {MaxAgeHours} hour(s).",
                deletedCount,
                options.Value.TempBlobMaxAgeHours);
        }

        return deletedCount;
    }
}
