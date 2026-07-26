namespace Shora.Application.Abstractions;

public sealed record GoogleTokenPayload(string Email, string Name, string Subject);

public interface IGoogleTokenValidator
{
    Task<GoogleTokenPayload?> ValidateAsync(string idToken, CancellationToken cancellationToken = default);
}
