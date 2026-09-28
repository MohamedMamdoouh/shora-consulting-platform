using Shora.Application.Bookings;
using Shora.Contracts.Booking;

namespace Shora.Tests.Unit.Bookings;

public class AdminBookingsQueryValidatorTests
{
    [Fact]
    public void Validate_accepts_default_query()
    {
        var result = AdminBookingsQueryValidator.Validate(new AdminBookingsQuery());

        Assert.True(result.IsValid);
        Assert.NotNull(result.Value);
        Assert.Equal(AdminBookingsQueryLimits.DefaultPage, result.Value!.Page);
        Assert.Equal(AdminBookingsQueryLimits.DefaultPageSize, result.Value.PageSize);
    }

    [Fact]
    public void Validate_rejects_page_below_one()
    {
        var result = AdminBookingsQueryValidator.Validate(new AdminBookingsQuery(Page: 0));

        Assert.False(result.IsValid);
        Assert.Contains("page", result.Errors.Keys);
    }

    [Fact]
    public void Validate_rejects_page_size_over_max()
    {
        var result = AdminBookingsQueryValidator.Validate(
            new AdminBookingsQuery(PageSize: AdminBookingsQueryLimits.MaxPageSize + 1));

        Assert.False(result.IsValid);
        Assert.Contains("pageSize", result.Errors.Keys);
    }

    [Fact]
    public void Validate_rejects_to_before_from()
    {
        var fromUtc = DateTime.UtcNow.AddDays(2);
        var result = AdminBookingsQueryValidator.Validate(
            new AdminBookingsQuery(FromUtc: fromUtc, ToUtc: fromUtc.AddHours(-1)));

        Assert.False(result.IsValid);
        Assert.Contains("toUtc", result.Errors.Keys);
    }
}
