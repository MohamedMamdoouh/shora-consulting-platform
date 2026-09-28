using Shora.Application.Options;

namespace Shora.Tests.Unit.Email;

public class EmailOptionsTests
{
    [Fact]
    public void IsConfigured_requires_api_key_and_from_address()
    {
        Assert.False(new EmailOptions().IsConfigured);
        Assert.False(new EmailOptions { ApiKey = "xkeysib-test" }.IsConfigured);
        Assert.False(new EmailOptions { FromAddress = "you@gmail.com" }.IsConfigured);
        Assert.True(new EmailOptions
        {
            ApiKey = "xkeysib-test",
            FromAddress = "you@gmail.com"
        }.IsConfigured);
    }
}
