using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Shora.Api.Filters;
using Shora.Api.Infrastructure;
using Shora.Application.Auth;
using Shora.Application.Common;
using Shora.Application.Common.Results;
using Shora.Application.Services;
using Shora.Contracts.Auth;
using Shora.Contracts.Common;
using Shora.Domain.Entities;
using Shora.Infrastructure.Services;

namespace Shora.Api.Controllers;

[Route("api/v{version:apiVersion}/auth")]
public sealed class AuthController : ApiControllerBase
{
    private readonly AuthService _authService;
    private readonly RefreshCookieService _refreshCookieService;
    private readonly UserManager<ApplicationUser> _userManager;

    public AuthController(
        AuthService authService,
        RefreshCookieService refreshCookieService,
        UserManager<ApplicationUser> userManager)
    {
        _authService = authService;
        _refreshCookieService = refreshCookieService;
        _userManager = userManager;
    }

    [HttpPost("signup")]
    [EnableRateLimiting(RateLimitPolicies.AuthCredential)]
    [EndpointName("Auth.SignUp")]
    [EndpointSummary("Register a new client account")]
    [ProducesResponseType(typeof(AuthResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> SignUp(SignUpRequest request, CancellationToken cancellationToken)
    {
        var result = await _authService.SignUpAsync(
            request,
            HttpContext.Connection.RemoteIpAddress?.ToString(),
            Request.Headers.UserAgent,
            cancellationToken);

        if (result.IsFailure)
        {
            return ToProblem(result.Error!);
        }

        SetRefreshCookie(result.Value!);
        return Ok(result.Value!.Response);
    }

    [HttpPost("login")]
    [EnableRateLimiting(RateLimitPolicies.AuthCredential)]
    [EndpointName("Auth.Login")]
    [EndpointSummary("Sign in with email and password")]
    [ProducesResponseType(typeof(AuthResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> Login(LoginRequest request, CancellationToken cancellationToken)
    {
        var result = await _authService.LoginAsync(
            request,
            HttpContext.Connection.RemoteIpAddress?.ToString(),
            Request.Headers.UserAgent,
            cancellationToken);

        if (result.IsFailure)
        {
            return ToProblem(result.Error!);
        }

        SetRefreshCookie(result.Value!);
        return Ok(result.Value!.Response);
    }

    [HttpPost("refresh")]
    [EnableRateLimiting(RateLimitPolicies.AuthRefresh)]
    [EndpointName("Auth.Refresh")]
    [EndpointSummary("Rotate refresh token and issue a new access token")]
    [ValidateAuthCookieOrigin]
    [ProducesResponseType(typeof(AuthResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> Refresh(CancellationToken cancellationToken)
    {
        var rawToken = _refreshCookieService.GetRefreshTokenFromRequest(Request);
        if (string.IsNullOrWhiteSpace(rawToken))
        {
            return ToProblem(Error.Unauthorized(
                ErrorCodes.Auth.RefreshTokenMissing,
                "Refresh token missing."));
        }

        var (rotation, auth) = await _authService.RefreshAsync(
            rawToken,
            HttpContext.Connection.RemoteIpAddress?.ToString(),
            Request.Headers.UserAgent,
            cancellationToken);

        if (auth.IsFailure)
        {
            _refreshCookieService.ClearRefreshTokenCookie(Response);
            return ToProblem(auth.Error!);
        }

        SetRefreshCookie(auth.Value!, rotation.NewToken!.ExpiresAtUtc);
        return Ok(auth.Value!.Response);
    }

    [HttpPost("logout")]
    [EndpointName("Auth.Logout")]
    [EndpointSummary("Revoke the current refresh token and clear the cookie")]
    [ValidateAuthCookieOrigin]
    [ProducesResponseType(typeof(MessageResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> Logout(CancellationToken cancellationToken)
    {
        var rawToken = _refreshCookieService.GetRefreshTokenFromRequest(Request);
        if (!string.IsNullOrWhiteSpace(rawToken))
        {
            await _authService.LogoutAsync(rawToken, cancellationToken);
        }

        _refreshCookieService.ClearRefreshTokenCookie(Response);
        return Ok(new MessageResponse("Logged out."));
    }

    [HttpPost("verify-email")]
    [EnableRateLimiting(RateLimitPolicies.AuthRecovery)]
    [EndpointName("Auth.VerifyEmail")]
    [EndpointSummary("Confirm email address with verification token")]
    [ProducesResponseType(typeof(AuthResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(MessageResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> VerifyEmail(VerifyEmailRequest request, CancellationToken cancellationToken)
    {
        var result = await _authService.VerifyEmailAsync(request, cancellationToken);
        if (result.IsFailure)
        {
            return ToProblem(result.Error!);
        }

        var message = result.Value == VerifyEmailOutcome.AlreadyVerified
            ? "Email already verified."
            : "Email verified.";

        var refreshed = await TryRefreshVerifiedUserSessionAsync(request.Email, cancellationToken);
        if (refreshed is not null)
        {
            return Ok(refreshed);
        }

        return Ok(new MessageResponse(message));
    }

    [HttpPost("resend-verification")]
    [EnableRateLimiting(RateLimitPolicies.AuthRecovery)]
    [EndpointName("Auth.ResendVerification")]
    [EndpointSummary("Resend email verification link")]
    [ProducesResponseType(typeof(MessageResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> ResendVerification(ResendVerificationRequest request, CancellationToken cancellationToken)
    {
        await _authService.ResendVerificationAsync(request, cancellationToken);
        return Ok(new MessageResponse("If an account exists and is unverified, a verification email was sent."));
    }

    [HttpPost("forgot-password")]
    [EnableRateLimiting(RateLimitPolicies.AuthRecovery)]
    [EndpointName("Auth.ForgotPassword")]
    [EndpointSummary("Request a password reset link")]
    [ProducesResponseType(typeof(MessageResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> ForgotPassword(ForgotPasswordRequest request, CancellationToken cancellationToken)
    {
        await _authService.ForgotPasswordAsync(request, cancellationToken);
        return Ok(new MessageResponse("If an account exists, a reset link was sent."));
    }

    [HttpPost("reset-password")]
    [EnableRateLimiting(RateLimitPolicies.AuthRecovery)]
    [EndpointName("Auth.ResetPassword")]
    [EndpointSummary("Reset password using email token")]
    [ProducesResponseType(typeof(MessageResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> ResetPassword(ResetPasswordRequest request, CancellationToken cancellationToken)
    {
        var result = await _authService.ResetPasswordAsync(request, cancellationToken);
        if (result.IsFailure)
        {
            return ToProblem(result.Error!);
        }

        return Ok(new MessageResponse("Password reset successful."));
    }

    [HttpPost("google")]
    [EnableRateLimiting(RateLimitPolicies.AuthCredential)]
    [EndpointName("Auth.GoogleSignIn")]
    [EndpointSummary("Sign in or register with Google ID token")]
    [ProducesResponseType(typeof(AuthResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> Google(GoogleSignInRequest request, CancellationToken cancellationToken)
    {
        var result = await _authService.GoogleSignInAsync(
            request,
            HttpContext.Connection.RemoteIpAddress?.ToString(),
            Request.Headers.UserAgent,
            cancellationToken);

        if (result.IsFailure)
        {
            return ToProblem(result.Error!);
        }

        SetRefreshCookie(result.Value!);
        return Ok(result.Value!.Response);
    }

    [Authorize]
    [HttpGet("me")]
    [EndpointName("Auth.GetCurrentUser")]
    [EndpointSummary("Get the authenticated user profile")]
    [ProducesResponseType(typeof(MeResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Me(CancellationToken cancellationToken)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user is null)
        {
            return ToProblem(Error.Unauthorized(
                ErrorCodes.Auth.UserNotFound,
                "User is not authenticated."));
        }

        var result = await _authService.BuildMeResponseAsync(user, cancellationToken);
        return FromResult(result);
    }

    private void SetRefreshCookie(AuthResult result, DateTime? expiresAtUtc = null)
    {
        var expires = expiresAtUtc ?? result.RefreshTokenExpiresAtUtc;
        _refreshCookieService.SetRefreshTokenCookie(Response, result.RefreshTokenRaw, expires);
    }

    private async Task<AuthResponse?> TryRefreshVerifiedUserSessionAsync(
        string email,
        CancellationToken cancellationToken)
    {
        var rawToken = _refreshCookieService.GetRefreshTokenFromRequest(Request);
        if (string.IsNullOrWhiteSpace(rawToken))
        {
            return null;
        }

        var user = await _userManager.FindByEmailAsync(email.Trim());
        if (user is null)
        {
            return null;
        }

        var (rotation, auth) = await _authService.RefreshAsync(
            rawToken,
            HttpContext.Connection.RemoteIpAddress?.ToString(),
            Request.Headers.UserAgent,
            cancellationToken);

        if (!auth.IsSuccess || rotation.UserId != user.Id)
        {
            return null;
        }

        SetRefreshCookie(auth.Value!, rotation.NewToken!.ExpiresAtUtc);
        return auth.Value!.Response;
    }
}
