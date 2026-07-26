import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Router } from '@angular/router';
import {
  BehaviorSubject,
  Observable,
  finalize,
  firstValueFrom,
  shareReplay,
  tap,
  throwError,
} from 'rxjs';
import { AuthResponse, AuthUser } from '@contracts/auth';
import { MessageResponse } from '@contracts/common';
import { environment } from '../../../environments/environment';

@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly http = inject(HttpClient);
  private readonly router = inject(Router);

  private accessToken: string | null = null;
  private readonly currentUserSubject = new BehaviorSubject<AuthUser | null>(null);
  readonly currentUser$ = this.currentUserSubject.asObservable();

  private refreshInFlight$: Observable<AuthResponse> | null = null;
  private isLoggingOut = false;
  private sessionExpiredHandled = false;
  private loginRedirectPromise: Promise<boolean> | null = null;
  private sessionGeneration = 0;

  getCurrentUser(): AuthUser | null {
    return this.currentUserSubject.value;
  }

  getAccessToken(): string | null {
    return this.accessToken;
  }

  isAuthenticated(): boolean {
    return this.accessToken !== null;
  }

  async initialize(): Promise<void> {
    try {
      await firstValueFrom(this.refresh());
    } catch {
      this.clearSession();
    }
  }

  signup(email: string, password: string, displayName?: string): Observable<AuthResponse> {
    return this.http
      .post<AuthResponse>(
        `${environment.apiBaseUrl}/auth/signup`,
        { email, password, displayName: displayName || null },
        { withCredentials: true },
      )
      .pipe(tap((response) => this.setSession(response)));
  }

  login(email: string, password: string): Observable<AuthResponse> {
    return this.http
      .post<AuthResponse>(
        `${environment.apiBaseUrl}/auth/login`,
        { email, password },
        { withCredentials: true },
      )
      .pipe(tap((response) => this.setSession(response)));
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

  verifyEmail(email: string, token: string): Observable<MessageResponse> {
    return this.http.post<MessageResponse>(`${environment.apiBaseUrl}/auth/verify-email`, {
      email,
      token,
    });
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

  redirectAfterLogin(role: string): Promise<boolean> {
    if (role === 'Admin') {
      return this.router.navigate(['/admin']);
    }

    return this.router.navigate(['/dashboard']);
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

  private setSession(response: AuthResponse): void {
    if (this.isLoggingOut) {
      return;
    }

    this.sessionExpiredHandled = false;
    this.accessToken = response.accessToken;
    this.currentUserSubject.next({
      displayName: response.displayName,
      role: response.role,
      emailConfirmed: response.emailConfirmed,
    });
  }

  private clearSession(): void {
    this.sessionGeneration++;
    this.accessToken = null;
    this.currentUserSubject.next(null);
  }
}

export type { AuthResponse, AuthUser };
