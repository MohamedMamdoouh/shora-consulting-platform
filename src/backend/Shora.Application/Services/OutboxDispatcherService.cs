using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Shora.Application.Abstractions;
using Shora.Application.Outbox;
using Shora.Domain.Entities;
using Shora.Domain.Enums;

namespace Shora.Application.Services;

public sealed class OutboxDispatcherService(
    IApplicationDbContext dbContext,
    IOutboxEmailRenderer emailRenderer,
    IEmailSender emailSender,
    IDateTimeProvider dateTimeProvider,
    ILogger<OutboxDispatcherService> logger)
{
    private const int BatchSize = 20;
    private const int MaxLastErrorLength = 2000;

    public async Task<int> RunAsync(CancellationToken cancellationToken = default)
    {
        var now = dateTimeProvider.UtcNow;

        var messages = await dbContext.OutboxMessages
            .Where(message =>
                message.Status == OutboxMessageStatus.Pending
                && message.NextAttemptAtUtc <= now)
            .OrderBy(message => message.NextAttemptAtUtc)
            .Take(BatchSize)
            .ToListAsync(cancellationToken);

        if (messages.Count == 0)
        {
            return 0;
        }

        var processedCount = 0;

        foreach (var message in messages)
        {
            if (await TryDispatchAsync(message, now, cancellationToken))
            {
                processedCount++;
            }
        }

        return processedCount;
    }

    private async Task<bool> TryDispatchAsync(
        OutboxMessage message,
        DateTime now,
        CancellationToken cancellationToken)
    {
        try
        {
            var renderResult = await emailRenderer.RenderAsync(message, cancellationToken);
            if (renderResult.IsFailure)
            {
                await RecordFailureAsync(message, renderResult.Error!.Message, now, cancellationToken);
                return false;
            }

            var rendered = renderResult.Value!;
            await emailSender.SendAsync(
                rendered.ToEmail,
                rendered.Subject,
                rendered.HtmlBody,
                cancellationToken);

            message.Status = OutboxMessageStatus.Processed;
            message.ProcessedAtUtc = now;
            message.LastError = null;
            await dbContext.SaveChangesAsync(cancellationToken);

            logger.LogInformation(
                "Outbox message {MessageId} delivered. Type={MessageType} AggregateId={AggregateId}",
                message.Id,
                message.MessageType,
                message.AggregateId);

            return true;
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            await RecordFailureAsync(message, exception.Message, now, cancellationToken);
            return false;
        }
    }

    private async Task RecordFailureAsync(
        OutboxMessage message,
        string error,
        DateTime now,
        CancellationToken cancellationToken)
    {
        message.AttemptCount++;
        message.LastError = TruncateError(error);

        if (message.AttemptCount >= OutboxRetryPolicy.MaxAttempts)
        {
            message.Status = OutboxMessageStatus.DeadLettered;

            logger.LogWarning(
                "Outbox message {MessageId} dead-lettered after {AttemptCount} attempts. Type={MessageType} AggregateId={AggregateId} Error={Error}",
                message.Id,
                message.AttemptCount,
                message.MessageType,
                message.AggregateId,
                message.LastError);
        }
        else
        {
            message.NextAttemptAtUtc = now + OutboxRetryPolicy.GetDelayAfterFailure(message.AttemptCount);

            logger.LogWarning(
                "Outbox message {MessageId} delivery failed (attempt {AttemptCount}/{MaxAttempts}). Next attempt at {NextAttemptAtUtc}. Error={Error}",
                message.Id,
                message.AttemptCount,
                OutboxRetryPolicy.MaxAttempts,
                message.NextAttemptAtUtc,
                message.LastError);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private static string TruncateError(string error) =>
        error.Length <= MaxLastErrorLength
            ? error
            : error[..MaxLastErrorLength];
}
