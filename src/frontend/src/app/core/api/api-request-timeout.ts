import { HttpContextToken } from '@angular/common/http';

export const API_REQUEST_TIMEOUT_MS = 30_000;
export const API_UPLOAD_TIMEOUT_MS = 120_000;

export const API_TIMEOUT_MS = new HttpContextToken<number>(() => API_REQUEST_TIMEOUT_MS);

export function isApiRequest(url: string): boolean {
  return url.startsWith('/api/v1') || url.includes('/api/v1/');
}

export function resolveApiRequestTimeoutMs(url: string, contextTimeoutMs: number): number | null {
  if (contextTimeoutMs === 0) {
    return null;
  }

  if (contextTimeoutMs !== API_REQUEST_TIMEOUT_MS) {
    return contextTimeoutMs;
  }

  if (url.includes('/receipt')) {
    return API_UPLOAD_TIMEOUT_MS;
  }

  return API_REQUEST_TIMEOUT_MS;
}
