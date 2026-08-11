namespace Shora.Application.Options;

public sealed class CorsOptions
{
    public const string SectionName = "Cors";

    public const string PolicyName = "SpaCors";

    private const string DefaultOrigin = "http://localhost:4200";

    public string[] AllowedOrigins { get; set; } = [];

    public string[] EffectiveOrigins =>
        AllowedOrigins is { Length: > 0 } ? AllowedOrigins : [DefaultOrigin];
}
