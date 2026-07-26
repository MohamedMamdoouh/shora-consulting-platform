using Shora.Contracts.Auth;

namespace Shora.Application.Auth;

public sealed record AuthResult(
    AuthResponse Response,
    string RefreshTokenRaw,
    DateTime RefreshTokenExpiresAtUtc);
