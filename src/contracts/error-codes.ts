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
} as const;
