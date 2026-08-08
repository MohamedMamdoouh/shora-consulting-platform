using Microsoft.EntityFrameworkCore;
using Shora.Application.Abstractions;
using Shora.Domain.Entities;

namespace Shora.Application.Services;

public sealed class JobHeartbeatService(
    IApplicationDbContext dbContext,
    IDateTimeProvider dateTimeProvider)
{
    private const int MaxErrorLength = 2000;

    public async Task RecordSuccessAsync(string jobName, CancellationToken cancellationToken = default)
    {
        var now = dateTimeProvider.UtcNow;
        var entry = await dbContext.JobRunHistories
            .FirstOrDefaultAsync(j => j.JobName == jobName, cancellationToken);

        if (entry is null)
        {
            dbContext.JobRunHistories.Add(new JobRunHistory
            {
                JobName = jobName,
                LastSuccessAtUtc = now
            });
        }
        else
        {
            entry.LastSuccessAtUtc = now;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task RecordFailureAsync(
        string jobName,
        string errorMessage,
        CancellationToken cancellationToken = default)
    {
        var now = dateTimeProvider.UtcNow;
        var truncatedError = TruncateError(errorMessage);
        var entry = await dbContext.JobRunHistories
            .FirstOrDefaultAsync(j => j.JobName == jobName, cancellationToken);

        if (entry is null)
        {
            dbContext.JobRunHistories.Add(new JobRunHistory
            {
                JobName = jobName,
                LastFailureAtUtc = now,
                LastError = truncatedError
            });
        }
        else
        {
            entry.LastFailureAtUtc = now;
            entry.LastError = truncatedError;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<DateTime?> GetLastSuccessAtUtcAsync(
        string jobName,
        CancellationToken cancellationToken = default)
    {
        return await dbContext.JobRunHistories
            .AsNoTracking()
            .Where(j => j.JobName == jobName)
            .Select(j => j.LastSuccessAtUtc)
            .FirstOrDefaultAsync(cancellationToken);
    }

    private static string TruncateError(string errorMessage)
    {
        if (string.IsNullOrEmpty(errorMessage))
        {
            return string.Empty;
        }

        return errorMessage.Length <= MaxErrorLength
            ? errorMessage
            : errorMessage[..MaxErrorLength];
    }
}
