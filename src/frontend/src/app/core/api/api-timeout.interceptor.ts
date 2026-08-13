import { HttpInterceptorFn } from '@angular/common/http';
import { timeout } from 'rxjs';
import {
  API_TIMEOUT_MS,
  isApiRequest,
  resolveApiRequestTimeoutMs,
} from './api-request-timeout';

export const apiTimeoutInterceptor: HttpInterceptorFn = (req, next) => {
  if (!isApiRequest(req.url)) {
    return next(req);
  }

  const timeoutMs = resolveApiRequestTimeoutMs(req.url, req.context.get(API_TIMEOUT_MS));
  if (timeoutMs === null) {
    return next(req);
  }

  return next(req).pipe(timeout(timeoutMs));
};
