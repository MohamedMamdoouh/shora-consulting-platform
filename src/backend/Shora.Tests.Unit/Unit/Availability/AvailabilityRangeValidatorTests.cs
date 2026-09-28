using Shora.Application.Availability;

namespace Shora.Tests.Unit.Availability;

public class AvailabilityRangeValidatorTests
{
    private static readonly DateTime UtcNow = new(2026, 7, 28, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Normalize_rejects_when_from_is_not_before_to()
    {
        var from = UtcNow;
        var to = UtcNow;

        var result = AvailabilityRangeValidator.Normalize(from, to, UtcNow);

        Assert.True(result.IsFailure);
        Assert.Equal("availability.invalid_range", result.Error!.Code);
    }

    [Fact]
    public void Normalize_rejects_when_range_exceeds_horizon()
    {
        var from = UtcNow;
        var to = from.AddDays(SlotGenerationConstants.HorizonWeeks * 7 + 1);

        var result = AvailabilityRangeValidator.Normalize(from, to, UtcNow);

        Assert.True(result.IsFailure);
        Assert.Equal("availability.range_too_large", result.Error!.Code);
    }

    [Fact]
    public void Normalize_clamps_past_from_to_now()
    {
        var from = UtcNow.AddDays(-2);
        var to = UtcNow.AddDays(7);

        var result = AvailabilityRangeValidator.Normalize(from, to, UtcNow);

        Assert.True(result.IsSuccess);
        Assert.Equal(UtcNow, result.Value.FromUtc);
        Assert.Equal(to, result.Value.ToUtc);
    }

    [Fact]
    public void Normalize_accepts_valid_future_range()
    {
        var from = UtcNow.AddHours(1);
        var to = UtcNow.AddDays(14);

        var result = AvailabilityRangeValidator.Normalize(from, to, UtcNow);

        Assert.True(result.IsSuccess);
        Assert.Equal(from, result.Value.FromUtc);
        Assert.Equal(to, result.Value.ToUtc);
    }
}
