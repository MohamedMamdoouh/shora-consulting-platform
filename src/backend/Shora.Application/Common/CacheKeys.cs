namespace Shora.Application.Common;

public static class CacheKeys
{
    public const string SettingsPublic = "settings:public";

    public const string AvailabilityPrefix = "availability:";

    public static string Availability(DateTime fromUtc, DateTime toUtc) =>
        $"{AvailabilityPrefix}{fromUtc:O}:{toUtc:O}";
}
