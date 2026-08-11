using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Shora.Application.Abstractions;
using Shora.Application.Options;
using Shora.Domain.Entities;
using Shora.Infrastructure.Data;

namespace Shora.Infrastructure.Services;

public sealed class RefreshTokenService(
    ApplicationDbContext context,
    IDateTimeProvider dateTimeProvider,
    IOptions<JwtOptions> jwtOptions) : IRefreshTokenService
{
    private static readonly TimeSpan GraceWindow = TimeSpan.FromSeconds(60);

    private readonly JwtOptions _jwtOptions = jwtOptions.Value;

    public async Task<RefreshTokenCreation> CreateAsync(
        Guid userId,
        string? createdByIp,
        string? userAgent,
        CancellationToken cancellationToken = default)
    {
        var rawToken = GenerateRawToken();
        var now = dateTimeProvider.UtcNow;
        var expiresAt = now.AddDays(_jwtOptions.RefreshTokenDays);

        context.RefreshTokens.Add(new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            TokenHash = RefreshCookieService.HashToken(rawToken),
            CreatedAtUtc = now,
            ExpiresAtUtc = expiresAt,
            CreatedByIp = createdByIp,
            UserAgent = userAgent
        });

        await context.SaveChangesAsync(cancellationToken);
        return new RefreshTokenCreation(rawToken, expiresAt);
    }

    public async Task<RefreshTokenRotationResult> RotateAsync(
        string rawToken,
        string? createdByIp,
        string? userAgent,
        CancellationToken cancellationToken = default)
    {
        var tokenHash = RefreshCookieService.HashToken(rawToken);
        var now = dateTimeProvider.UtcNow;

        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
        var result = await RotateCoreAsync(tokenHash, now, createdByIp, userAgent, cancellationToken);
        if (result.Status == RefreshTokenStatus.Invalid && result.NewToken is null)
        {
            await transaction.RollbackAsync(cancellationToken);
        }
        else
        {
            await transaction.CommitAsync(cancellationToken);
        }

        return result;
    }

    private async Task<RefreshTokenRotationResult> RotateCoreAsync(
        string tokenHash,
        DateTime now,
        string? createdByIp,
        string? userAgent,
        CancellationToken cancellationToken)
    {
        var stored = await FindTokenForUpdateAsync(tokenHash, cancellationToken);
        if (stored is null)
        {
            return new RefreshTokenRotationResult(RefreshTokenStatus.Invalid);
        }

        if (stored.ExpiresAtUtc <= now)
        {
            stored.RevokedAtUtc ??= now;
            await context.SaveChangesAsync(cancellationToken);
            return new RefreshTokenRotationResult(RefreshTokenStatus.Expired);
        }

        if (stored.RevokedAtUtc is not null)
        {
            var revokedAge = now - stored.RevokedAtUtc.Value;
            if (revokedAge <= GraceWindow && stored.ReplacedByTokenHash is not null)
            {
                var graceToken = IssueGraceWindowSuccessor(
                    stored,
                    now,
                    createdByIp,
                    userAgent);

                await context.SaveChangesAsync(cancellationToken);
                return new RefreshTokenRotationResult(
                    RefreshTokenStatus.Success,
                    graceToken,
                    stored.UserId);
            }

            if (revokedAge > GraceWindow)
            {
                await RevokeAllActiveForUserInternalAsync(stored.UserId, now, cancellationToken);
                return new RefreshTokenRotationResult(RefreshTokenStatus.ReuseDetected);
            }

            return new RefreshTokenRotationResult(RefreshTokenStatus.Invalid);
        }

        var newRawToken = GenerateRawToken();
        var newHash = RefreshCookieService.HashToken(newRawToken);
        var newExpiresAt = now.AddDays(_jwtOptions.RefreshTokenDays);

        stored.RevokedAtUtc = now;
        stored.ReplacedByTokenHash = newHash;

        context.RefreshTokens.Add(new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = stored.UserId,
            TokenHash = newHash,
            CreatedAtUtc = now,
            ExpiresAtUtc = newExpiresAt,
            CreatedByIp = createdByIp,
            UserAgent = userAgent
        });

        await context.SaveChangesAsync(cancellationToken);

        return new RefreshTokenRotationResult(
            RefreshTokenStatus.Success,
            new RefreshTokenCreation(newRawToken, newExpiresAt),
            stored.UserId);
    }

    public async Task<bool> RevokeAsync(string rawToken, CancellationToken cancellationToken = default)
    {
        var tokenHash = RefreshCookieService.HashToken(rawToken);
        var stored = await context.RefreshTokens.FirstOrDefaultAsync(t => t.TokenHash == tokenHash, cancellationToken);
        if (stored is null || stored.RevokedAtUtc is not null)
        {
            return false;
        }

        stored.RevokedAtUtc = dateTimeProvider.UtcNow;
        await context.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task RevokeAllForUserAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        await RevokeAllActiveForUserInternalAsync(userId, dateTimeProvider.UtcNow, cancellationToken);
    }

    private async Task RevokeAllActiveForUserInternalAsync(
        Guid userId,
        DateTime now,
        CancellationToken cancellationToken)
    {
        var activeTokens = await context.RefreshTokens
            .Where(t => t.UserId == userId && t.RevokedAtUtc == null)
            .ToListAsync(cancellationToken);

        foreach (var token in activeTokens)
        {
            token.RevokedAtUtc = now;
        }

        if (activeTokens.Count > 0)
        {
            await context.SaveChangesAsync(cancellationToken);
        }
    }

    private Task<RefreshToken?> FindTokenForUpdateAsync(string tokenHash, CancellationToken cancellationToken)
    {
        // Serialize concurrent refresh attempts on the same token (multi-tab race).
        return context.RefreshTokens
            .FromSqlInterpolated($"SELECT * FROM \"RefreshTokens\" WHERE \"TokenHash\" = {tokenHash} FOR UPDATE")
            .FirstOrDefaultAsync(cancellationToken);
    }

    private RefreshTokenCreation IssueGraceWindowSuccessor(
        RefreshToken revokedToken,
        DateTime now,
        string? createdByIp,
        string? userAgent)
    {
        var newRawToken = GenerateRawToken();
        var newHash = RefreshCookieService.HashToken(newRawToken);
        var newExpiresAt = now.AddDays(_jwtOptions.RefreshTokenDays);

        context.RefreshTokens.Add(new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = revokedToken.UserId,
            TokenHash = newHash,
            CreatedAtUtc = now,
            ExpiresAtUtc = newExpiresAt,
            CreatedByIp = createdByIp,
            UserAgent = userAgent
        });

        revokedToken.ReplacedByTokenHash = newHash;
        return new RefreshTokenCreation(newRawToken, newExpiresAt);
    }

    private static string GenerateRawToken()
    {
        var bytes = RandomNumberGenerator.GetBytes(32);
        return Convert.ToBase64String(bytes)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }
}
