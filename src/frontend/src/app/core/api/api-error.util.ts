import { HttpErrorResponse } from '@angular/common/http';
import { ProblemDetails } from '@contracts/common';

function problemFromHttpError(error: HttpErrorResponse): ProblemDetails | null {
  const body = error.error;
  if (!body || typeof body !== 'object') {
    return null;
  }

  return body as ProblemDetails;
}

export function readApiError(error: unknown, fallback: string): string {
  if (!(error instanceof HttpErrorResponse)) {
    return fallback;
  }

  const problem = problemFromHttpError(error);
  return problem?.detail ?? problem?.title ?? fallback;
}

export function readApiErrorCode(error: unknown): string | undefined {
  if (!(error instanceof HttpErrorResponse)) {
    return undefined;
  }

  return problemFromHttpError(error)?.code;
}
