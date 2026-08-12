import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { ErrorCatalogEntry, ErrorCatalogListResponse } from '@contracts/error-catalog';
import { environment } from '../../../environments/environment';

@Injectable({ providedIn: 'root' })
export class ErrorReferenceService {
  private readonly http = inject(HttpClient);

  list(): Observable<ErrorCatalogListResponse> {
    return this.http.get<ErrorCatalogListResponse>(`${environment.apiBaseUrl}/errors`);
  }

  get(code: string): Observable<ErrorCatalogEntry> {
    return this.http.get<ErrorCatalogEntry>(
      `${environment.apiBaseUrl}/errors/${encodeURIComponent(code)}`,
    );
  }
}
