export const ErrorCodes = {
  Auth: {
    InvalidCredentials: 'auth.invalid_credentials',
    DuplicateEmail: 'auth.duplicate_email',
    RefreshTokenMissing: 'auth.refresh_token_missing',
    RefreshTokenInvalid: 'auth.refresh_token_invalid',
    RefreshTokenReuse: 'auth.refresh_token_reuse',
    GoogleSignInFailed: 'auth.google_sign_in_failed',
    VerificationFailed: 'auth.verification_failed',
    ResetFailed: 'auth.reset_failed',
    UserNotFound: 'auth.user_not_found',
    ValidationFailed: 'auth.validation_failed',
  },
  General: {
    Validation: 'general.validation',
    Unexpected: 'general.unexpected',
    Forbidden: 'general.forbidden',
  },
  Availability: {
    InvalidRange: 'availability.invalid_range',
    RangeTooLarge: 'availability.range_too_large',
  },
  Booking: {
    SlotUnavailable: 'booking.slot_unavailable',
    HoldCapExceeded: 'booking.hold_cap_exceeded',
    EmailNotVerified: 'booking.email_not_verified',
    ContactPhoneRequired: 'booking.contact_phone_required',
    ContactPhoneInvalid: 'booking.contact_phone_invalid',
    NotFound: 'booking.not_found',
    Forbidden: 'booking.forbidden',
    InvalidStatus: 'booking.invalid_status',
  },
  Cancellation: {
    TooLate: 'cancellation.too_late',
    ReopenExhausted: 'cancellation.reopen_exhausted',
  },
} as const;
