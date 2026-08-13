import { HttpClient } from '@angular/common/http';
import { Injectable, inject, signal } from '@angular/core';
import { Router } from '@angular/router';
import { Observable, finalize, firstValueFrom, shareReplay, tap, throwError } from 'rxjs';
import { AuthResponse, AuthUser, MeResponse } from '@contracts/auth';
import { MessageResponse } from '@contracts/common';
import { environment } from '../../../environments/environment';
import { resolvePostLoginRedirect, sanitizeAuthReturnUrl } from './auth-redirect.util';

const USER_EMAIL_STORAGE_KEY = 'shora.auth.email';

@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly http = inject(HttpClient);
  private readonly router = inject(Router);

  private accessToken: string | null = null;
  private readonly currentUserState = signal<AuthUser | null>(null);
  readonly currentUser = this.currentUserState.asReadonly();

  private refreshInFlight$: Observable<AuthResponse> | null = null;
  private isLoggingOut = false;
  private sessionExpiredHandled = false;
  private loginRedirectPromise: Promise<boolean> | null = null;
  private sessionGeneration = 0;

  getCurrentUser(): AuthUser | null {
    return this.currentUserState();
  }

  getAccessToken(): string | null {
    return this.accessToken;
  }

  isAuthenticated(): boolean {
    return this.accessToken !== null;
  }

  getUserEmail(): string | null {
    return sessionStorage.getItem(USER_EMAIL_STORAGE_KEY);
  }

  async initialize(): Promise<void> {
    try {
      await firstValueFrom(this.refresh());
      await this.syncCurrentUser();
    } catch {
      this.clearSession();
    }
  }

  async syncCurrentUser(): Promise<void> {
    if (!this.isAuthenticated()) {
      return;
    }

    try {
      const profile = await firstValueFrom(
        this.http.get<MeResponse>(`${environment.apiBaseUrl}/auth/me`),
      );
      this.patchCurrentUser(profile);
    } catch {
      // Session may have expired; leave state unchanged and let guards/interceptor handle it.
    }
  }

  signup(email: string, password: string, displayName?: string): Observable<AuthResponse> {
    return this.http
      .post<AuthResponse>(
        `${environment.apiBaseUrl}/auth/signup`,
        { email, password, displayName: displayName || null },
        { withCredentials: true },
      )
      .pipe(tap((response) => this.setSession(response, email)));
  }

  login(email: string, password: string): Observable<AuthResponse> {
    return this.http
      .post<AuthResponse>(
        `${environment.apiBaseUrl}/auth/login`,
        { email, password },
        { withCredentials: true },
      )
      .pipe(tap((response) => this.setSession(response, email)));
  }

  googleSignIn(idToken: string): Observable<AuthResponse> {
    return this.http
      .post<AuthResponse>(
        `${environment.apiBaseUrl}/auth/google`,
        { idToken },
        { withCredentials: true },
      )
      .pipe(tap((response) => this.setSession(response)));
  }

  refresh(): Observable<AuthResponse> {
    if (this.isLoggingOut) {
      return throwError(() => new Error('Logout in progress'));
    }

    if (this.refreshInFlight$) {
      return this.refreshInFlight$;
    }

    const generation = this.sessionGeneration;

    this.refreshInFlight$ = this.http
      .post<AuthResponse>(`${environment.apiBaseUrl}/auth/refresh`, null, { withCredentials: true })
      .pipe(
        tap((response) => {
          if (generation !== this.sessionGeneration) {
            return;
          }
          this.setSession(response);
        }),
        finalize(() => {
          this.refreshInFlight$ = null;
        }),
        shareReplay({ bufferSize: 1, refCount: false }),
      );

    return this.refreshInFlight$;
  }

  verifyEmail(email: string, token: string): Observable<MessageResponse | AuthResponse> {
    return this.http
      .post<MessageResponse | AuthResponse>(
        `${environment.apiBaseUrl}/auth/verify-email`,
        { email, token },
        { withCredentials: true },
      )
      .pipe(
        tap((response) => {
          if ('accessToken' in response) {
            this.setSession(response, email);
          }
        }),
      );
  }

  resendVerification(email: string): Observable<MessageResponse> {
    return this.http.post<MessageResponse>(`${environment.apiBaseUrl}/auth/resend-verification`, {
      email,
    });
  }

  forgotPassword(email: string): Observable<MessageResponse> {
    return this.http.post<MessageResponse>(`${environment.apiBaseUrl}/auth/forgot-password`, {
      email,
    });
  }

  resetPassword(email: string, token: string, newPassword: string): Observable<MessageResponse> {
    return this.http.post<MessageResponse>(`${environment.apiBaseUrl}/auth/reset-password`, {
      email,
      token,
      newPassword,
    });
  }

  async logout(): Promise<void> {
    if (this.isLoggingOut) {
      return;
    }

    this.isLoggingOut = true;
    this.refreshInFlight$ = null;

    try {
      await firstValueFrom(
        this.http.post(`${environment.apiBaseUrl}/auth/logout`, null, { withCredentials: true }),
      );
    } finally {
      this.clearSession();
      this.isLoggingOut = false;
      await this.redirectToLogin();
    }
  }

  async handleSessionExpired(): Promise<void> {
    if (this.sessionExpiredHandled) {
      return;
    }

    this.sessionExpiredHandled = true;
    this.refreshInFlight$ = null;
    this.clearSession();

    await this.redirectToLogin({ reason: 'sessionExpired' });
  }

  redirectAfterLogin(role: string, returnUrl?: string | null): Promise<boolean> {
    const redirect = resolvePostLoginRedirect(role, returnUrl);
    return redirect.kind === 'url'
      ? this.router.navigateByUrl(redirect.url)
      : this.router.navigate(redirect.commands);
  }

  sanitizeReturnUrl(returnUrl?: string | null): string | null {
    return sanitizeAuthReturnUrl(returnUrl);
  }

  private async redirectToLogin(options?: { reason?: string }): Promise<boolean> {
    if (this.loginRedirectPromise) {
      return this.loginRedirectPromise;
    }

    const queryParams = options?.reason ? { reason: options.reason } : undefined;
    const isOnLogin = this.router.url.startsWith('/auth/login');

    const navigation = isOnLogin
      ? this.router.navigate(['/auth/login'], { queryParams, queryParamsHandling: 'merge' })
      : this.router.navigate(['/auth/login'], { queryParams });

    this.loginRedirectPromise = navigation.finally(() => {
      this.loginRedirectPromise = null;
    });

    return this.loginRedirectPromise;
  }

  private patchCurrentUser(profile: MeResponse): void {
    const current = this.currentUserState();
    if (!current) {
      return;
    }

    this.currentUserState.set({
      displayName: profile.displayName,
      role: profile.role,
      emailConfirmed: profile.emailConfirmed,
      email: profile.email || current.email,
    });

    if (profile.email) {
      sessionStorage.setItem(USER_EMAIL_STORAGE_KEY, profile.email);
    }
  }

  private setSession(response: AuthResponse, email?: string): void {
    if (this.isLoggingOut) {
      return;
    }

    this.sessionExpiredHandled = false;
    this.accessToken = response.accessToken;
    this.currentUserState.set({
      displayName: response.displayName,
      role: response.role,
      emailConfirmed: response.emailConfirmed,
      email: email ?? this.getUserEmail() ?? undefined,
    });

    if (email) {
      sessionStorage.setItem(USER_EMAIL_STORAGE_KEY, email);
    }
  }

  private clearSession(): void {
    this.sessionGeneration++;
    this.accessToken = null;
    this.currentUserState.set(null);
    sessionStorage.removeItem(USER_EMAIL_STORAGE_KEY);
  }
}

export type { AuthResponse, AuthUser };
