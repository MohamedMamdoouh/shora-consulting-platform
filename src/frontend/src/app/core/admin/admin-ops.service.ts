import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { AdminOpsAlertsResponse, AdminOpsRunbooksResponse } from '@contracts/ops';
import { environment } from '../../../environments/environment';

@Injectable({ providedIn: 'root' })
export class AdminOpsService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = `${environment.apiBaseUrl}/admin/ops`;

  getAlerts(): Observable<AdminOpsAlertsResponse> {
    return this.http.get<AdminOpsAlertsResponse>(`${this.baseUrl}/alerts`);
  }

  getRunbooks(): Observable<AdminOpsRunbooksResponse> {
    return this.http.get<AdminOpsRunbooksResponse>(`${this.baseUrl}/runbooks`);
  }
}
