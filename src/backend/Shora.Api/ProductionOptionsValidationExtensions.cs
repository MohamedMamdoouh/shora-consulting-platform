using Microsoft.Extensions.Options;
using Shora.Application.Options;

namespace Shora.Api;

public static class ProductionOptionsValidationExtensions
{
    public static IServiceCollection AddProductionOptionsValidation(this IServiceCollection services)
    {
        services.AddSingleton<IValidateOptions<JwtOptions>, JwtOptionsValidator>();
        services.AddSingleton<IValidateOptions<FrontendOptions>, FrontendOptionsValidator>();
        services.AddSingleton<IValidateOptions<CorsOptions>, CorsOptionsValidator>();
        services.AddSingleton<IValidateOptions<EmailOptions>, EmailOptionsValidator>();
        services.AddSingleton<IValidateOptions<StorageOptions>, StorageOptionsValidator>();

        services.AddOptions<JwtOptions>().ValidateOnStart();
        services.AddOptions<FrontendOptions>().ValidateOnStart();
        services.AddOptions<CorsOptions>().ValidateOnStart();
        services.AddOptions<EmailOptions>().ValidateOnStart();
        services.AddOptions<StorageOptions>().ValidateOnStart();

        return services;
    }
}
