import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { ApiCacheStore } from './api-cache';

@Injectable({ providedIn: 'root' })
export class ApiCacheService {
  private readonly http = inject(HttpClient);
  private readonly store = new ApiCacheStore(<T>(url: string) => this.http.get<T>(url));

  getCached<T>(url: string, ttlMs: number): Observable<T> {
    return this.store.getCached(url, ttlMs);
  }

  invalidate(url: string): void {
    this.store.invalidate(url);
  }

  invalidateUrlPrefix(urlPrefix: string): void {
    this.store.invalidateUrlPrefix(urlPrefix);
  }
}
