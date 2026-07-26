namespace Shora.Application.Abstractions;

public sealed record RefreshTokenCreation(string RawToken, DateTime ExpiresAtUtc);

public enum RefreshTokenStatus
{
    Success,
    Invalid,
    Expired,
    ReuseDetected
}

public sealed record RefreshTokenRotationResult(
    RefreshTokenStatus Status,
    RefreshTokenCreation? NewToken = null,
    Guid? UserId = null);

public interface IRefreshTokenService
{
    Task<RefreshTokenCreation> CreateAsync(
        Guid userId,
        string? createdByIp,
        string? userAgent,
        CancellationToken cancellationToken = default);

    Task<RefreshTokenRotationResult> RotateAsync(
        string rawToken,
        string? createdByIp,
        string? userAgent,
        CancellationToken cancellationToken = default);

    Task<bool> RevokeAsync(string rawToken, CancellationToken cancellationToken = default);

    Task RevokeAllForUserAsync(Guid userId, CancellationToken cancellationToken = default);
}
