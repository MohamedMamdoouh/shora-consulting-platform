namespace Shora.Contracts.Auth;

public sealed record AuthResponse(
    string AccessToken,
    string DisplayName,
    string Role,
    bool EmailConfirmed);

public sealed record SignUpRequest(string Email, string Password, string? DisplayName);

public sealed record LoginRequest(string Email, string Password);

public sealed record GoogleSignInRequest(string IdToken);

public sealed record VerifyEmailRequest(string Email, string Token);

public sealed record ResendVerificationRequest(string Email);

public sealed record ForgotPasswordRequest(string Email);

public sealed record ResetPasswordRequest(string Email, string Token, string NewPassword);

public sealed record MeResponse(
    string DisplayName,
    string Role,
    bool EmailConfirmed,
    string Email);
