using System.Net;
using Microsoft.AspNetCore.Http;
using Shora.Api.Infrastructure;

namespace Shora.Tests.Unit.Infrastructure;

public sealed class RateLimitPartitionFactoryTests
{
    [Fact]
    public void FixedWindowByIp_uses_ip_only_partition_key()
    {
        var context = new DefaultHttpContext();
        context.Connection.RemoteIpAddress = IPAddress.Parse("203.0.113.10");

        var partition = RateLimitPartitionFactory.FixedWindowByIp(
            context,
            "auth-credential",
            permitLimit: 5,
            window: TimeSpan.FromMinutes(1));

        Assert.Equal("auth-credential:ip:203.0.113.10", partition.PartitionKey);
    }
}
