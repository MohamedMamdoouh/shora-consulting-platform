using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using Shora.Application.Abstractions;
using Shora.Application.Auth;
using Shora.Application.Common;
using Shora.Application.Common.Results;
using Shora.Contracts.Auth;
using Shora.Domain.Entities;
using Shora.Domain.Enums;

namespace Shora.Application.Services;

public sealed class AuthService
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IJwtTokenService _jwtTokenService;
    private readonly IRefreshTokenService _refreshTokenService;
    private readonly AuthEmailService _authEmailService;
    private readonly ILogger<AuthService> _logger;

    public AuthService(
        UserManager<ApplicationUser> userManager,
        IJwtTokenService jwtTokenService,
        IRefreshTokenService refreshTokenService,
        AuthEmailService authEmailService,
        ILogger<AuthService> logger)
    {
        _userManager = userManager;
        _jwtTokenService = jwtTokenService;
        _refreshTokenService = refreshTokenService;
        _authEmailService = authEmailService;
        _logger = logger;
    }

    public async Task<Result<AuthResult>> SignUpAsync(
        SignUpRequest request,
        string? ipAddress,
        string? userAgent,
        CancellationToken cancellationToken = default)
    {
        var email = request.Email.Trim();
        var existing = await _userManager.FindByEmailAsync(email);
        if (existing is not null)
        {
            return Error.Conflict(
                ErrorCodes.Auth.DuplicateEmail,
                "Email is already registered.");
        }

        var displayName = string.IsNullOrWhiteSpace(request.DisplayName)
            ? email.Split('@')[0]
            : request.DisplayName.Trim();

        var user = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            UserName = email,
            Email = email,
            DisplayName = displayName,
            EmailConfirmed = false,
            Role = UserRole.Client
        };

        var createResult = await _userManager.CreateAsync(user, request.Password);
        if (!createResult.Succeeded)
        {
            return MapIdentityErrors(createResult);
        }

        await _userManager.AddToRoleAsync(user, AppRoles.Client);
        await TrySendAuthEmailAsync(
            () => _authEmailService.SendVerificationAsync(user, cancellationToken),
            "verification",
            user.Email);

        var authResult = await IssueTokensAsync(user, ipAddress, userAgent, cancellationToken);
        return authResult;
    }

    public async Task<Result<AuthResult>> LoginAsync(
        LoginRequest request,
        string? ipAddress,
        string? userAgent,
        CancellationToken cancellationToken = default)
    {
        var user = await _userManager.FindByEmailAsync(request.Email.Trim());
        if (user is null || !await _userManager.CheckPasswordAsync(user, request.Password))
        {
            return Error.Unauthorized(
                ErrorCodes.Auth.InvalidCredentials,
                "Invalid email or password.");
        }

        var authResult = await IssueTokensAsync(user, ipAddress, userAgent, cancellationToken);
        return authResult;
    }

    public async Task<(RefreshTokenRotationResult Rotation, Result<AuthResult> Auth)> RefreshAsync(
        string rawToken,
        string? ipAddress,
        string? userAgent,
        CancellationToken cancellationToken = default)
    {
        var rotation = await _refreshTokenService.RotateAsync(rawToken, ipAddress, userAgent, cancellationToken);
        if (rotation.Status == RefreshTokenStatus.ReuseDetected)
        {
            return (rotation, Error.Unauthorized(
                ErrorCodes.Auth.RefreshTokenReuse,
                "Session invalidated due to token reuse."));
        }

        if (rotation.Status != RefreshTokenStatus.Success || rotation.UserId is null || rotation.NewToken is null)
        {
            return (rotation, Error.Unauthorized(
                ErrorCodes.Auth.RefreshTokenInvalid,
                "Invalid or expired refresh token."));
        }

        var user = await _userManager.FindByIdAsync(rotation.UserId.Value.ToString());
        if (user is null)
        {
            return (new RefreshTokenRotationResult(RefreshTokenStatus.Invalid),
                Error.Unauthorized(
                    ErrorCodes.Auth.RefreshTokenInvalid,
                    "Invalid or expired refresh token."));
        }

        var roles = await _userManager.GetRolesAsync(user);
        var role = roles.FirstOrDefault() ?? AppRoles.Client;
        var accessToken = _jwtTokenService.CreateAccessToken(user, role);
        var response = BuildAuthResponse(accessToken, user, role);

        return (rotation, new AuthResult(response, rotation.NewToken.RawToken, rotation.NewToken.ExpiresAtUtc));
    }

    public Task<bool> LogoutAsync(string? rawToken, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(rawToken))
        {
            return Task.FromResult(false);
        }

        return _refreshTokenService.RevokeAsync(rawToken, cancellationToken);
    }

    public async Task<Result<VerifyEmailOutcome>> VerifyEmailAsync(
        VerifyEmailRequest request,
        CancellationToken cancellationToken = default)
    {
        var user = await _userManager.FindByEmailAsync(request.Email.Trim());
        if (user is null)
        {
            return Error.Validation(
                ErrorCodes.Auth.VerificationFailed,
                "Invalid verification request.");
        }

        if (user.EmailConfirmed)
        {
            return VerifyEmailOutcome.AlreadyVerified;
        }

        var result = await _userManager.ConfirmEmailAsync(user, NormalizeIdentityToken(request.Token));
        if (!result.Succeeded)
        {
            return Error.Validation(
                ErrorCodes.Auth.VerificationFailed,
                "Invalid or expired verification token.");
        }

        return VerifyEmailOutcome.NewlyVerified;
    }

    public async Task ResendVerificationAsync(
        ResendVerificationRequest request,
        CancellationToken cancellationToken = default)
    {
        var user = await _userManager.FindByEmailAsync(request.Email.Trim());
        if (user is null || user.EmailConfirmed)
        {
            return;
        }

        await TrySendAuthEmailAsync(
            () => _authEmailService.SendVerificationAsync(user, cancellationToken),
            "verification",
            user.Email);
    }

    public async Task ResendVerificationForUserAsync(
        ApplicationUser user,
        CancellationToken cancellationToken = default)
    {
        if (user.EmailConfirmed)
        {
            return;
        }

        await TrySendAuthEmailAsync(
            () => _authEmailService.SendVerificationAsync(user, cancellationToken),
            "verification",
            user.Email);
    }

    public async Task ForgotPasswordAsync(
        ForgotPasswordRequest request,
        CancellationToken cancellationToken = default)
    {
        var user = await _userManager.FindByEmailAsync(request.Email.Trim());
        if (user is null)
        {
            return;
        }

        await TrySendAuthEmailAsync(
            () => _authEmailService.SendPasswordResetAsync(user, cancellationToken),
            "password reset",
            user.Email);
    }

    public async Task<Result> ResetPasswordAsync(
        ResetPasswordRequest request,
        CancellationToken cancellationToken = default)
    {
        var user = await _userManager.FindByEmailAsync(request.Email.Trim());
        if (user is null)
        {
            return Error.Validation(
                ErrorCodes.Auth.ResetFailed,
                "Invalid reset request.");
        }

        var result = await _userManager.ResetPasswordAsync(
            user,
            NormalizeIdentityToken(request.Token),
            request.NewPassword);
        if (!result.Succeeded)
        {
            return Error.Validation(
                ErrorCodes.Auth.ResetFailed,
                string.Join(' ', result.Errors.Select(e => e.Description)));
        }

        await _refreshTokenService.RevokeAllForUserAsync(user.Id, cancellationToken);
        return Result.Success();
    }

    public async Task<Result<AuthResponse>> BuildAuthResponseForUserAsync(
        ApplicationUser user,
        CancellationToken cancellationToken = default)
    {
        var roles = await _userManager.GetRolesAsync(user);
        var role = roles.FirstOrDefault() ?? AppRoles.Client;
        var accessToken = _jwtTokenService.CreateAccessToken(user, role);
        return BuildAuthResponse(accessToken, user, role);
    }

    public async Task<Result<MeResponse>> BuildMeResponseAsync(
        ApplicationUser user,
        CancellationToken cancellationToken = default)
    {
        var roles = await _userManager.GetRolesAsync(user);
        var role = roles.FirstOrDefault() ?? AppRoles.Client;

        return new MeResponse(
            user.DisplayName,
            role,
            user.EmailConfirmed,
            user.Email ?? string.Empty);
    }

    private async Task<AuthResult> IssueTokensAsync(
        ApplicationUser user,
        string? ipAddress,
        string? userAgent,
        CancellationToken cancellationToken)
    {
        var roles = await _userManager.GetRolesAsync(user);
        var role = roles.FirstOrDefault() ?? AppRoles.Client;
        var accessToken = _jwtTokenService.CreateAccessToken(user, role);
        var refresh = await _refreshTokenService.CreateAsync(user.Id, ipAddress, userAgent, cancellationToken);
        return new AuthResult(BuildAuthResponse(accessToken, user, role), refresh.RawToken, refresh.ExpiresAtUtc);
    }

    private static AuthResponse BuildAuthResponse(string accessToken, ApplicationUser user, string role)
    {
        return new AuthResponse(accessToken, user.DisplayName, role, user.EmailConfirmed);
    }

    private async Task TrySendAuthEmailAsync(
        Func<Task> sendEmail,
        string emailKind,
        string? recipientEmail)
    {
        try
        {
            await sendEmail();
        }
        catch (Exception exception)
        {
            _logger.LogError(
                exception,
                "Failed to send {EmailKind} email to {RecipientEmail}.",
                emailKind,
                recipientEmail);
        }
    }

    private static Error MapIdentityErrors(IdentityResult identityResult)
    {
        var message = string.Join(' ', identityResult.Errors.Select(e => e.Description));
        var code = identityResult.Errors.Any(e => e.Code == "DuplicateEmail")
            ? ErrorCodes.Auth.DuplicateEmail
            : ErrorCodes.Auth.ValidationFailed;

        var kind = code == ErrorCodes.Auth.DuplicateEmail ? ErrorKind.Conflict : ErrorKind.Validation;
        return new Error(code, message, kind);
    }

    private static string NormalizeIdentityToken(string token)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return token;
        }

        var normalized = token.Trim();
        if (normalized.Contains(' ', StringComparison.Ordinal))
        {
            normalized = normalized.Replace(' ', '+');
        }

        return normalized;
    }
}
