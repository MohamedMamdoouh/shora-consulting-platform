namespace Shora.Application.Options;

public sealed class RefreshCookieOptions
{
    public const string SectionName = "RefreshCookie";

    public string CookieName { get; set; } = "shora_refresh";

    public string Path { get; set; } = "/api";
}
