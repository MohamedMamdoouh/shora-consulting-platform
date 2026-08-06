using Shora.Application.Availability;
using Shora.Contracts.Availability;

namespace Shora.Tests.Unit.Availability;

public class AvailabilityWindowValidatorTests
{
    private static CreateAvailabilityWindowRequest ValidRequest() =>
        new(
            DayOfWeek.Monday,
            new TimeSpan(16, 0, 0),
            new TimeSpan(21, 0, 0),
            IsActive: true);

    [Fact]
    public void ValidateCreate_accepts_valid_window()
    {
        var result = AvailabilityWindowValidator.ValidateCreate(ValidRequest());

        Assert.True(result.IsValid);
        Assert.Equal(DayOfWeek.Monday, result.Value!.DayOfWeek);
    }

    [Fact]
    public void ValidateCreate_rejects_end_before_start()
    {
        var request = ValidRequest() with { EndTime = new TimeSpan(15, 0, 0) };

        var result = AvailabilityWindowValidator.ValidateCreate(request);

        Assert.False(result.IsValid);
        Assert.Contains("endTime", result.Errors.Keys);
    }

    [Fact]
    public void ValidateCreate_rejects_equal_start_and_end()
    {
        var request = ValidRequest() with { EndTime = new TimeSpan(16, 0, 0) };

        var result = AvailabilityWindowValidator.ValidateCreate(request);

        Assert.False(result.IsValid);
        Assert.Contains("endTime", result.Errors.Keys);
    }
}
