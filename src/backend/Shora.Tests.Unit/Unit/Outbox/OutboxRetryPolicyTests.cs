using Shora.Application.Outbox;

namespace Shora.Tests.Unit.Outbox;

public class OutboxRetryPolicyTests
{
    [Theory]
    [InlineData(1, 5)]
    [InlineData(2, 15)]
    [InlineData(3, 30)]
    [InlineData(4, 60)]
    [InlineData(5, 120)]
    [InlineData(6, 240)]
    [InlineData(7, 960)]
    [InlineData(8, 960)]
    public void GetDelayAfterFailure_returns_expected_minutes(int attemptCount, int expectedMinutes)
    {
        var delay = OutboxRetryPolicy.GetDelayAfterFailure(attemptCount);

        Assert.Equal(TimeSpan.FromMinutes(expectedMinutes), delay);
    }

    [Fact]
    public void GetDelayAfterFailure_returns_zero_for_non_positive_attempt_count()
    {
        Assert.Equal(TimeSpan.Zero, OutboxRetryPolicy.GetDelayAfterFailure(0));
        Assert.Equal(TimeSpan.Zero, OutboxRetryPolicy.GetDelayAfterFailure(-1));
    }

    [Fact]
    public void Cumulative_delay_before_eighth_attempt_is_about_one_day()
    {
        var totalDelay = Enumerable.Range(1, OutboxRetryPolicy.MaxAttempts - 1)
            .Select(OutboxRetryPolicy.GetDelayAfterFailure)
            .Aggregate(TimeSpan.Zero, static (sum, delay) => sum + delay);

        Assert.InRange(totalDelay.TotalHours, 23, 25);
    }
}
