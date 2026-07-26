import { HttpErrorResponse } from '@angular/common/http';
import { readApiError, readApiErrorCode } from './api-error.util';

describe('readApiError', () => {
  it('returns detail from problem body', () => {
    const error = new HttpErrorResponse({
      error: { detail: 'Email is already registered.' },
      status: 409,
    });

    expect(readApiError(error, 'fallback')).toBe('Email is already registered.');
  });

  it('returns title when detail is missing', () => {
    const error = new HttpErrorResponse({
      error: { title: 'Conflict' },
      status: 409,
    });

    expect(readApiError(error, 'fallback')).toBe('Conflict');
  });

  it('returns fallback for empty or malformed body', () => {
    const emptyBody = new HttpErrorResponse({ error: null, status: 500 });
    const stringBody = new HttpErrorResponse({ error: 'Server error', status: 500 });

    expect(readApiError(emptyBody, 'fallback')).toBe('fallback');
    expect(readApiError(stringBody, 'fallback')).toBe('fallback');
  });

  it('returns fallback for non-HttpErrorResponse', () => {
    expect(readApiError(new Error('network'), 'fallback')).toBe('fallback');
  });
});

describe('readApiErrorCode', () => {
  it('returns code extension from problem body', () => {
    const error = new HttpErrorResponse({
      error: { code: 'auth.duplicate_email' },
      status: 409,
    });

    expect(readApiErrorCode(error)).toBe('auth.duplicate_email');
  });

  it('returns undefined for non-HttpErrorResponse', () => {
    expect(readApiErrorCode(new Error('network'))).toBeUndefined();
  });
});
