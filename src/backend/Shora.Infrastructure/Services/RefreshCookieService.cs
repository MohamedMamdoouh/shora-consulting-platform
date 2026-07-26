using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Shora.Application.Options;

namespace Shora.Infrastructure.Services;

public sealed class RefreshCookieService(
    IHostEnvironment hostEnvironment,
    IOptions<RefreshCookieOptions> options)
{
    private readonly RefreshCookieOptions _options = options.Value;

    public void SetRefreshTokenCookie(HttpResponse response, string rawToken, DateTime expiresAtUtc)
    {
        response.Cookies.Append(_options.CookieName, rawToken, BuildCookieOptions(expiresAtUtc));
    }

    public void ClearRefreshTokenCookie(HttpResponse response)
    {
        response.Cookies.Delete(_options.CookieName, new CookieOptions
        {
            Path = _options.Path,
            HttpOnly = true,
            Secure = !hostEnvironment.IsDevelopment(),
            SameSite = SameSiteMode.Strict
        });
    }

    public string? GetRefreshTokenFromRequest(HttpRequest request)
    {
        return request.Cookies.TryGetValue(_options.CookieName, out var value) ? value : null;
    }

    private CookieOptions BuildCookieOptions(DateTime expiresAtUtc)
    {
        var secureCookies = !hostEnvironment.IsDevelopment();

        return new CookieOptions
        {
            HttpOnly = true,
            Secure = secureCookies,
            SameSite = SameSiteMode.Strict,
            Path = _options.Path,
            Expires = new DateTimeOffset(expiresAtUtc)
        };
    }

    public static string HashToken(string rawToken)
    {
        var hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(rawToken));
        return Convert.ToHexString(hashBytes);
    }
}
