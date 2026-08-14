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
        public const string WindowNotFound = "availability.window_not_found";
        public const string BlockedDateNotFound = "availability.blocked_date_not_found";
        public const string BlockedRangeConflictsWithBookings = "availability.blocked_range_conflicts_with_bookings";
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
        public const string InvalidDecisionReason = "cancellation.invalid_decision_reason";
    }

    public static class Payment
    {
        public const string NotFound = "payment.not_found";
        public const string Forbidden = "payment.forbidden";
        public const string InvalidStatus = "payment.invalid_status";
        public const string UploadDeadlinePassed = "payment.upload_deadline_passed";
        public const string InvalidReceiptFile = "payment.invalid_receipt_file";
        public const string ReceiptTooLarge = "payment.receipt_too_large";
        public const string InvalidMethod = "payment.invalid_method";
        public const string NoPendingReceipt = "payment.no_pending_receipt";
        public const string ReceiptFinalizeFailed = "payment.receipt_finalize_failed";
        public const string ReceiptNotReviewable = "payment.receipt_not_reviewable";
        public const string InvalidDeclineReason = "payment.invalid_decline_reason";
        public const string RefundNotDue = "payment.refund_not_due";
        public const string InvalidRefundReference = "payment.invalid_refund_reference";
    }

    public static class General
    {
        public const string Validation = "general.validation";
        public const string Unexpected = "general.unexpected";
        public const string Forbidden = "general.forbidden";
    }

    public static class Errors
    {
        public const string NotFound = "errors.not_found";
    }
}
