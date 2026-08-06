import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { BlockedDate, CreateBlockedDateRequest } from '@contracts/availability';
import { environment } from '../../../environments/environment';

@Injectable({ providedIn: 'root' })
export class AdminBlockedDateService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = `${environment.apiBaseUrl}/admin/blocked-dates`;

  listBlockedDates(): Observable<BlockedDate[]> {
    return this.http.get<BlockedDate[]>(this.baseUrl);
  }

  createBlockedDate(request: CreateBlockedDateRequest): Observable<BlockedDate> {
    return this.http.post<BlockedDate>(this.baseUrl, request);
  }

  deleteBlockedDate(id: string): Observable<void> {
    return this.http.delete<void>(`${this.baseUrl}/${id}`);
  }
}
