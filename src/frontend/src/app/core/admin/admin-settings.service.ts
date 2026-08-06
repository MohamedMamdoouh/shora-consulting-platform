import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { AdminSettings, UpdateAdminSettingsRequest } from '@contracts/settings';
import { environment } from '../../../environments/environment';

@Injectable({ providedIn: 'root' })
export class AdminSettingsService {
  private readonly http = inject(HttpClient);

  getSettings(): Observable<AdminSettings> {
    return this.http.get<AdminSettings>(`${environment.apiBaseUrl}/admin/settings`);
  }

  updateSettings(request: UpdateAdminSettingsRequest): Observable<AdminSettings> {
    return this.http.put<AdminSettings>(`${environment.apiBaseUrl}/admin/settings`, request);
  }
}
