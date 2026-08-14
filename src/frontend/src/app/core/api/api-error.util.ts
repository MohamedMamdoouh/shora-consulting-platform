import { HttpErrorResponse } from '@angular/common/http';
import { ProblemDetails } from '@contracts/common';
import { API_ERROR_MESSAGES } from './api-error-messages';

function problemFromHttpError(error: HttpErrorResponse): ProblemDetails | null {
  const body = error.error;
  if (!body || typeof body !== 'object') {
    return null;
  }

  return body as ProblemDetails;
}

/**
 * Resolves an Arabic message for an API error. Backend `title`/`detail` text is English and is
 * never surfaced to the UI — known error codes map to Arabic via `API_ERROR_MESSAGES`, otherwise
 * the caller-supplied Arabic `fallback` is used.
 */
export function readApiError(error: unknown, fallback: string): string {
  if (!(error instanceof HttpErrorResponse)) {
    return fallback;
  }

  const code = problemFromHttpError(error)?.code;
  return (code && API_ERROR_MESSAGES[code]) ?? fallback;
}

export function readApiErrorCode(error: unknown): string | undefined {
  if (!(error instanceof HttpErrorResponse)) {
    return undefined;
  }

  return problemFromHttpError(error)?.code;
}

export function readValidationErrors(error: unknown): Record<string, string[]> | null {
  if (!(error instanceof HttpErrorResponse)) {
    return null;
  }

  return problemFromHttpError(error)?.errors ?? null;
}

export function readConflictingBookingIds(error: unknown): string[] | null {
  if (!(error instanceof HttpErrorResponse)) {
    return null;
  }

  const ids = problemFromHttpError(error)?.conflictingBookingIds;
  if (!Array.isArray(ids)) {
    return null;
  }

  return ids.filter((id): id is string => typeof id === 'string');
}
