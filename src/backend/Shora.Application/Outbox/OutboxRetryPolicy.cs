namespace Shora.Application.Outbox;

internal static class OutboxRetryPolicy
{
    public const int MaxAttempts = 8;

    private static readonly TimeSpan[] DelaysAfterFailure =
    [
        TimeSpan.FromMinutes(5),
        TimeSpan.FromMinutes(15),
        TimeSpan.FromMinutes(30),
        TimeSpan.FromHours(1),
        TimeSpan.FromHours(2),
        TimeSpan.FromHours(4),
        TimeSpan.FromHours(16)
    ];

    public static TimeSpan GetDelayAfterFailure(int attemptCount)
    {
        if (attemptCount <= 0)
        {
            return TimeSpan.Zero;
        }

        var index = Math.Min(attemptCount - 1, DelaysAfterFailure.Length - 1);
        return DelaysAfterFailure[index];
    }
}
