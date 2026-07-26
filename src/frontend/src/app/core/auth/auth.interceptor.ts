import { HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { catchError, switchMap, throwError } from 'rxjs';
import { AuthService } from './auth.service';

export const authInterceptor: HttpInterceptorFn = (req, next) => {
  const auth = inject(AuthService);
  const token = auth.getAccessToken();

  let authReq = req.clone({ withCredentials: true });
  if (token) {
    authReq = authReq.clone({
      setHeaders: { Authorization: `Bearer ${token}` },
    });
  }

  return next(authReq).pipe(
    catchError((error: HttpErrorResponse) => {
      const isAuthEndpoint =
        req.url.includes('/auth/login') ||
        req.url.includes('/auth/signup') ||
        req.url.includes('/auth/refresh') ||
        req.url.includes('/auth/logout');

      if (error.status !== 401 || isAuthEndpoint || req.headers.has('X-Retry')) {
        return throwError(() => error);
      }

      if (!auth.isAuthenticated()) {
        return throwError(() => error);
      }

      return auth.refresh().pipe(
        switchMap(() => {
          const retryToken = auth.getAccessToken();
          const retryReq = req.clone({
            withCredentials: true,
            setHeaders: {
              ...(retryToken ? { Authorization: `Bearer ${retryToken}` } : {}),
              'X-Retry': 'true',
            },
          });
          return next(retryReq);
        }),
        catchError(() => {
          void auth.handleSessionExpired();
          return throwError(() => error);
        }),
      );
    }),
  );
};
