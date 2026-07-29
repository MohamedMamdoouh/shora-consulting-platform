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

    public static class Settings
    {
        public const string NotFound = "settings.not_found";
    }

    public static class Availability
    {
        public const string InvalidRange = "availability.invalid_range";
        public const string RangeTooLarge = "availability.range_too_large";
    }

    public static class Booking
    {
        public const string SlotUnavailable = "booking.slot_unavailable";
        public const string HoldCapExceeded = "booking.hold_cap_exceeded";
        public const string EmailNotVerified = "booking.email_not_verified";
        public const string ContactPhoneRequired = "booking.contact_phone_required";
        public const string ContactPhoneInvalid = "booking.contact_phone_invalid";
        public const string NotFound = "booking.not_found";
        public const string Forbidden = "booking.forbidden";
        public const string InvalidStatus = "booking.invalid_status";
    }

    public static class Cancellation
    {
        public const string TooLate = "cancellation.too_late";
        public const string ReopenExhausted = "cancellation.reopen_exhausted";
    }

    public static class General
    {
        public const string Validation = "general.validation";
        public const string Unexpected = "general.unexpected";
        public const string Forbidden = "general.forbidden";
    }
}
