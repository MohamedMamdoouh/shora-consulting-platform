using Shora.Application.Bookings;

namespace Shora.Tests.Unit.Bookings;

public class PhoneNormalizerTests
{
    [Theory]
    [InlineData("01012345678", "+201012345678")]
    [InlineData("+201012345678", "+201012345678")]
    [InlineData("201012345678", "+201012345678")]
    public void NormalizeToE164_accepts_valid_egypt_mobile_numbers(string input, string expected)
    {
        var result = PhoneNormalizer.NormalizeToE164(input);

        Assert.True(result.IsSuccess);
        Assert.Equal(expected, result.Value);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("123")]
    [InlineData("not-a-phone")]
    public void NormalizeToE164_rejects_invalid_numbers(string? input)
    {
        var result = PhoneNormalizer.NormalizeToE164(input);

        Assert.True(result.IsFailure);
        Assert.Equal("booking.contact_phone_invalid", result.Error!.Code);
    }
}
