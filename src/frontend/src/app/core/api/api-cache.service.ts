import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, shareReplay } from 'rxjs';

interface CacheEntry<T> {
  expiresAt: number;
  data$: Observable<T>;
}

@Injectable({ providedIn: 'root' })
export class ApiCacheService {
  private readonly http = inject(HttpClient);
  private readonly entries = new Map<string, CacheEntry<unknown>>();

  getCached<T>(key: string, url: string, ttlMs: number): Observable<T> {
    const now = Date.now();
    const existing = this.entries.get(key) as CacheEntry<T> | undefined;

    if (existing && existing.expiresAt > now) {
      return existing.data$;
    }

    const data$ = this.http.get<T>(url).pipe(
      shareReplay({ bufferSize: 1, refCount: false }),
    );

    this.entries.set(key, { expiresAt: now + ttlMs, data$ });
    return data$;
  }

  invalidate(key: string): void {
    this.entries.delete(key);
  }

  invalidatePrefix(prefix: string): void {
    for (const key of this.entries.keys()) {
      if (key.startsWith(prefix)) {
        this.entries.delete(key);
      }
    }
  }
}
