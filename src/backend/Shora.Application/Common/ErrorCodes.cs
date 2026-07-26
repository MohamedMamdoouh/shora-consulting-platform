namespace Shora.Application.Common;

public static class ErrorCodes
{
    public static class Auth
    {
        public const string InvalidCredentials = "auth.invalid_credentials";
        public const string DuplicateEmail = "auth.duplicate_email";
        public const string RefreshTokenMissing = "auth.refresh_token_missing";
        public const string RefreshTokenInvalid = "auth.refresh_token_invalid";
        public const string RefreshTokenReuse = "auth.refresh_token_reuse";
        public const string GoogleSignInFailed = "auth.google_sign_in_failed";
        public const string VerificationFailed = "auth.verification_failed";
        public const string ResetFailed = "auth.reset_failed";
        public const string UserNotFound = "auth.user_not_found";
        public const string ValidationFailed = "auth.validation_failed";
    }

    public static class General
    {
        public const string Validation = "general.validation";
        public const string Unexpected = "general.unexpected";
        public const string Forbidden = "general.forbidden";
    }
}
