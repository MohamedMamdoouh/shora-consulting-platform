import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import {
  AvailabilityWindow,
  CreateAvailabilityWindowRequest,
  UpdateAvailabilityWindowRequest,
} from '@contracts/availability';
import { environment } from '../../../environments/environment';

@Injectable({ providedIn: 'root' })
export class AdminAvailabilityService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = `${environment.apiBaseUrl}/admin/availability-windows`;

  listWindows(): Observable<AvailabilityWindow[]> {
    return this.http.get<AvailabilityWindow[]>(this.baseUrl);
  }

  createWindow(request: CreateAvailabilityWindowRequest): Observable<AvailabilityWindow> {
    return this.http.post<AvailabilityWindow>(this.baseUrl, request);
  }

  updateWindow(id: string, request: UpdateAvailabilityWindowRequest): Observable<AvailabilityWindow> {
    return this.http.put<AvailabilityWindow>(`${this.baseUrl}/${id}`, request);
  }

  deleteWindow(id: string): Observable<void> {
    return this.http.delete<void>(`${this.baseUrl}/${id}`);
  }
}
