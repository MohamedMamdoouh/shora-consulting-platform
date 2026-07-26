import { HttpErrorResponse } from '@angular/common/http';
import { firstValueFrom, Observable, of, throwError } from 'rxjs';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { ApiCacheStore } from './api-cache';

describe('ApiCacheStore', () => {
  let httpGet: ReturnType<typeof vi.fn<(url: string) => Observable<unknown>>>;
  let store: ApiCacheStore;

  const settingsUrl = '/api/v1/settings/public';
  const availabilityUrlA = '/api/v1/availability?from=2026-01-01&to=2026-01-08';
  const availabilityUrlB = '/api/v1/availability?to=2026-01-08&from=2026-01-01';
  const ttlMs = 60_000;

  beforeEach(() => {
    httpGet = vi.fn();
    store = new ApiCacheStore(<T>(url: string) => httpGet(url) as Observable<T>);
  });

  afterEach(() => {
    vi.useRealTimers();
  });

  it('fetches on first call and caches within TTL', async () => {
    httpGet.mockReturnValue(of({ sessionPrice: 500 }));

    const first = firstValueFrom(store.getCached<{ sessionPrice: number }>(settingsUrl, ttlMs));
    await expect(first).resolves.toEqual({ sessionPrice: 500 });
    expect(httpGet).toHaveBeenCalledTimes(1);
    expect(httpGet).toHaveBeenCalledWith(settingsUrl);

    const second = firstValueFrom(store.getCached<{ sessionPrice: number }>(settingsUrl, ttlMs));
    await expect(second).resolves.toEqual({ sessionPrice: 500 });
    expect(httpGet).toHaveBeenCalledTimes(1);
  });

  it('refetches after TTL expires', async () => {
    vi.useFakeTimers();
    vi.setSystemTime(new Date('2026-01-01T00:00:00Z'));
    httpGet.mockReturnValueOnce(of({ sessionPrice: 500 })).mockReturnValueOnce(of({ sessionPrice: 600 }));

    await firstValueFrom(store.getCached<{ sessionPrice: number }>(settingsUrl, ttlMs));
    vi.advanceTimersByTime(ttlMs + 1);

    const second = firstValueFrom(store.getCached<{ sessionPrice: number }>(settingsUrl, ttlMs));
    await expect(second).resolves.toEqual({ sessionPrice: 600 });
    expect(httpGet).toHaveBeenCalledTimes(2);
  });

  it('evicts failed requests and refetches on the next call', async () => {
    httpGet
      .mockReturnValueOnce(
        throwError(() => new HttpErrorResponse({ status: 500, statusText: 'Server Error' })),
      )
      .mockReturnValueOnce(of({ sessionPrice: 500 }));

    const first = firstValueFrom(store.getCached(settingsUrl, ttlMs)).catch((error) => error);
    await expect(first).resolves.toMatchObject({ status: 500 });

    const second = firstValueFrom(store.getCached<{ sessionPrice: number }>(settingsUrl, ttlMs));
    await expect(second).resolves.toEqual({ sessionPrice: 500 });
    expect(httpGet).toHaveBeenCalledTimes(2);
  });

  it('shares one cache entry for the same URL with different query order', async () => {
    httpGet.mockReturnValue(of({ slots: ['09:00'] }));

    await firstValueFrom(store.getCached<{ slots: string[] }>(availabilityUrlA, ttlMs));

    const second = firstValueFrom(store.getCached<{ slots: string[] }>(availabilityUrlB, ttlMs));
    await expect(second).resolves.toEqual({ slots: ['09:00'] });
    expect(httpGet).toHaveBeenCalledTimes(1);
    expect(httpGet).toHaveBeenCalledWith(availabilityUrlA);
  });

  it('invalidateUrlPrefix removes matching entries only', async () => {
    httpGet
      .mockReturnValueOnce(of({ sessionPrice: 500 }))
      .mockReturnValueOnce(of({ slots: ['09:00'] }))
      .mockReturnValueOnce(of({ slots: ['10:00'] }));

    await firstValueFrom(store.getCached<{ sessionPrice: number }>(settingsUrl, ttlMs));
    await firstValueFrom(store.getCached<{ slots: string[] }>(availabilityUrlA, ttlMs));

    store.invalidateUrlPrefix('/api/v1/availability');

    const refetchedAvailability = firstValueFrom(
      store.getCached<{ slots: string[] }>(availabilityUrlA, ttlMs),
    );
    await expect(refetchedAvailability).resolves.toEqual({ slots: ['10:00'] });

    const cachedSettings = firstValueFrom(store.getCached<{ sessionPrice: number }>(settingsUrl, ttlMs));
    await expect(cachedSettings).resolves.toEqual({ sessionPrice: 500 });
    expect(httpGet).toHaveBeenCalledTimes(3);
  });

  it('invalidate removes a single cached URL', async () => {
    httpGet.mockReturnValueOnce(of({ sessionPrice: 500 })).mockReturnValueOnce(of({ sessionPrice: 600 }));

    await firstValueFrom(store.getCached<{ sessionPrice: number }>(settingsUrl, ttlMs));
    store.invalidate(settingsUrl);

    const second = firstValueFrom(store.getCached<{ sessionPrice: number }>(settingsUrl, ttlMs));
    await expect(second).resolves.toEqual({ sessionPrice: 600 });
    expect(httpGet).toHaveBeenCalledTimes(2);
  });
});
