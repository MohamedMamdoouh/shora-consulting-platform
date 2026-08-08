using System.Net;
using System.Text;
using Microsoft.AspNetCore.Http;
using Shora.Api.Infrastructure;
using Shora.Api.Middleware;

namespace Shora.Tests.Unit.Infrastructure;

public sealed class RateLimitPartitionFactoryTests
{
    [Fact]
    public async Task Auth_email_partition_key_does_not_include_raw_email()
    {
        var partitionKey = await GetAuthEmailPartitionKeyAsync("sensitive-target@example.test");

        Assert.StartsWith("auth-credential:ip:203.0.113.10:email-bucket:", partitionKey);
        Assert.DoesNotContain("sensitive-target", partitionKey, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("example.test", partitionKey, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Auth_email_partition_key_normalizes_email_before_bucketing()
    {
        var mixedCasePartitionKey = await GetAuthEmailPartitionKeyAsync("  User.Name@Example.TEST  ");
        var normalizedPartitionKey = await GetAuthEmailPartitionKeyAsync("user.name@example.test");

        Assert.Equal(normalizedPartitionKey, mixedCasePartitionKey);
    }

    [Fact]
    public async Task Auth_email_partition_keys_are_bounded_for_unique_email_inputs()
    {
        var partitionKeys = new HashSet<string>();
        var emailCount = RateLimitPartitionFactory.EmailPartitionBucketCount + 128;

        for (var index = 0; index < emailCount; index++)
        {
            partitionKeys.Add(await GetAuthEmailPartitionKeyAsync($"attacker-{index}@example.test"));
        }

        Assert.True(
            partitionKeys.Count <= RateLimitPartitionFactory.EmailPartitionBucketCount,
            $"Expected at most {RateLimitPartitionFactory.EmailPartitionBucketCount} keys but found {partitionKeys.Count}.");
    }

    private static async Task<string> GetAuthEmailPartitionKeyAsync(string email)
    {
        string? partitionKey = null;
        var cache = new RateLimiterCache();
        var middleware = new AuthRateLimitEmailMiddleware(context =>
        {
            var partition = RateLimitPartitionFactory.FixedWindowByIpAndEmail(
                context,
                "auth-credential",
                permitLimit: 5,
                window: TimeSpan.FromMinutes(1),
                cache);

            partitionKey = partition.PartitionKey;
            return Task.CompletedTask;
        });

        var context = new DefaultHttpContext();
        context.Connection.RemoteIpAddress = IPAddress.Parse("203.0.113.10");
        context.SetEndpoint(new Endpoint(
            _ => Task.CompletedTask,
            new EndpointMetadataCollection(new ExtractAuthEmailForRateLimitAttribute()),
            "auth endpoint"));
        context.Request.ContentType = "application/json";
        context.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes($$"""{"email":"{{email}}"}"""));

        await middleware.InvokeAsync(context);

        Assert.NotNull(partitionKey);
        return partitionKey;
    }
}
