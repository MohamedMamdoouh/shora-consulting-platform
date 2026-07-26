using Shora.Application.Abstractions;
using Shora.Application.Common;
using Shora.Application.Options;

namespace Shora.Api;

public static class CachingDependencyInjection
{
    public static IServiceCollection AddShoraCaching(this IServiceCollection services, IConfiguration configuration)
    {
        var cacheOptions = configuration.GetSection(CacheOptions.SectionName).Get<CacheOptions>() ?? new CacheOptions();

        services.AddSingleton<ICacheInvalidator, Infrastructure.CacheInvalidator>();

        services.AddOutputCache(options =>
        {
            options.AddPolicy(CachePolicies.PublicSettings, builder => builder
                .Expire(cacheOptions.SettingsPublicTtl)
                .Tag(CacheOutputTags.PublicSettings));

            options.AddPolicy(CachePolicies.PublicAvailability, builder => builder
                .Expire(cacheOptions.AvailabilityTtl)
                .SetVaryByQuery("from", "to")
                .Tag(CacheOutputTags.PublicAvailability));
        });

        return services;
    }
}
