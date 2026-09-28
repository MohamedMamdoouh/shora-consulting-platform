using Shora.Application.Earnings;
using Shora.Contracts.Payments;

namespace Shora.Tests.Unit.Earnings;

public class AdminEarningsQueryValidatorTests
{
    [Fact]
    public void Validate_accepts_empty_query()
    {
        var result = AdminEarningsQueryValidator.Validate(new AdminEarningsQuery());

        Assert.True(result.IsValid);
        Assert.NotNull(result.Value);
        Assert.Null(result.Value!.FromUtc);
        Assert.Null(result.Value.ToUtc);
    }

    [Fact]
    public void Validate_rejects_to_before_from()
    {
        var fromUtc = DateTime.UtcNow.AddDays(2);
        var result = AdminEarningsQueryValidator.Validate(
            new AdminEarningsQuery(FromUtc: fromUtc, ToUtc: fromUtc.AddHours(-1)));

        Assert.False(result.IsValid);
        Assert.Contains("toUtc", result.Errors.Keys);
    }
}
