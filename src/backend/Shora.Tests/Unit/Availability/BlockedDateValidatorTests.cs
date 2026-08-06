using Shora.Application.Availability;
using Shora.Contracts.Availability;

namespace Shora.Tests.Unit.Availability;

public class BlockedDateValidatorTests
{
    [Fact]
    public void ValidateCreate_accepts_valid_range()
    {
        var request = new CreateBlockedDateRequest(
            DateTime.UtcNow.AddDays(1),
            DateTime.UtcNow.AddDays(1).AddHours(8),
            "Vacation");

        var result = BlockedDateValidator.ValidateCreate(request);

        Assert.True(result.IsValid);
        Assert.NotNull(result.Value);
        Assert.Equal(request.StartUtc, result.Value!.StartUtc);
        Assert.Equal(request.EndUtc, result.Value.EndUtc);
        Assert.Equal(request.Reason, result.Value.Reason);
    }

    [Fact]
    public void ValidateCreate_rejects_end_before_start()
    {
        var startUtc = DateTime.UtcNow.AddDays(2);
        var request = new CreateBlockedDateRequest(startUtc, startUtc.AddHours(-1), null);

        var result = BlockedDateValidator.ValidateCreate(request);

        Assert.False(result.IsValid);
        Assert.Contains("endUtc", result.Errors.Keys);
    }

    [Fact]
    public void ValidateCreate_rejects_reason_that_is_too_long()
    {
        var request = new CreateBlockedDateRequest(
            DateTime.UtcNow.AddDays(3),
            DateTime.UtcNow.AddDays(3).AddHours(2),
            new string('x', 501));

        var result = BlockedDateValidator.ValidateCreate(request);

        Assert.False(result.IsValid);
        Assert.Contains("reason", result.Errors.Keys);
    }
}
