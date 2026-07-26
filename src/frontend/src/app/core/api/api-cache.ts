import { Observable, ReplaySubject, share, tap } from 'rxjs';

interface CacheEntry<T> {
  expiresAt: number;
  data$: Observable<T>;
}

export function normalizeUrl(url: string): string {
  try {
    const parsed = new URL(url, 'http://local');
    const params = [...parsed.searchParams.entries()].sort(([a], [b]) => a.localeCompare(b));
    const search = new URLSearchParams(params).toString();
    return parsed.pathname + (search ? `?${search}` : '');
  } catch {
    return url;
  }
}

export class ApiCacheStore {
  private readonly entries = new Map<string, CacheEntry<unknown>>();

  constructor(private readonly httpGet: <T>(url: string) => Observable<T>) {}

  getCached<T>(url: string, ttlMs: number): Observable<T> {
    const cacheKey = normalizeUrl(url);
    const now = Date.now();
    const existing = this.entries.get(cacheKey) as CacheEntry<T> | undefined;

    if (existing && existing.expiresAt > now) {
      return existing.data$;
    }

    const data$ = this.httpGet<T>(url).pipe(
      tap({
        error: () => this.entries.delete(cacheKey),
      }),
      share({
        connector: () => new ReplaySubject<T>(1),
        resetOnError: true,
        resetOnComplete: false,
      }),
    );

    this.entries.set(cacheKey, { expiresAt: now + ttlMs, data$ });
    return data$;
  }

  invalidate(url: string): void {
    this.entries.delete(normalizeUrl(url));
  }

  invalidateUrlPrefix(urlPrefix: string): void {
    const normalizedPrefix = normalizeUrl(urlPrefix);
    for (const key of this.entries.keys()) {
      if (key.startsWith(normalizedPrefix)) {
        this.entries.delete(key);
      }
    }
  }
}
