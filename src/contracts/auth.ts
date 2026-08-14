export interface AuthResponse {
  accessToken: string;
  displayName: string;
  role: string;
  emailConfirmed: boolean;
}

export interface SignUpRequest {
  email: string;
  password: string;
  displayName?: string | null;
}

export interface LoginRequest {
  email: string;
  password: string;
}

export interface GoogleSignInRequest {
  idToken: string;
}

export interface VerifyEmailRequest {
  email: string;
  token: string;
}

export interface ResendVerificationRequest {
  email: string;
}

export interface ForgotPasswordRequest {
  email: string;
}

export interface ResetPasswordRequest {
  email: string;
  token: string;
  newPassword: string;
}

export interface MeResponse {
  displayName: string;
  role: string;
  emailConfirmed: boolean;
  email: string;
}

export interface AuthUser {
  displayName: string;
  role: string;
  emailConfirmed: boolean;
  email?: string;
}
