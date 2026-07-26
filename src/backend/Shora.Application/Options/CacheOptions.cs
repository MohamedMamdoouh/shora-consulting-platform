namespace Shora.Application.Options;

public sealed class CacheOptions
{
    public const string SectionName = "Cache";

    public bool Enabled { get; set; } = true;

    public int SettingsPublicTtlSeconds { get; set; } = 300;

    public int AvailabilityTtlSeconds { get; set; } = 30;

    public TimeSpan SettingsPublicTtl => TimeSpan.FromSeconds(SettingsPublicTtlSeconds);

    public TimeSpan AvailabilityTtl => TimeSpan.FromSeconds(AvailabilityTtlSeconds);
}
