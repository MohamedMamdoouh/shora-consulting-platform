using Google.Apis.Auth;
using Microsoft.Extensions.Options;
using Shora.Application.Abstractions;
using Shora.Application.Options;

namespace Shora.Infrastructure.Services;

public sealed class GoogleTokenValidator(IOptions<GoogleOptions> options) : IGoogleTokenValidator
{
    public async Task<GoogleTokenPayload?> ValidateAsync(string idToken, CancellationToken cancellationToken = default)
    {
        var clientId = options.Value.ClientId;
        if (string.IsNullOrWhiteSpace(clientId))
        {
            throw new InvalidOperationException("Google:ClientId is not configured.");
        }

        var settings = new GoogleJsonWebSignature.ValidationSettings
        {
            Audience = [clientId]
        };

        try
        {
            var payload = await GoogleJsonWebSignature.ValidateAsync(idToken, settings);
            if (string.IsNullOrWhiteSpace(payload.Email))
            {
                return null;
            }

            return new GoogleTokenPayload(
                payload.Email,
                payload.Name ?? payload.Email.Split('@')[0],
                payload.Subject);
        }
        catch (InvalidJwtException)
        {
            return null;
        }
    }
}
