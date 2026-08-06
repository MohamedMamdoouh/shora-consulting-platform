import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { AdminEarningsQuery, AdminEarningsResponse } from '@contracts/earnings';
import { environment } from '../../../environments/environment';

@Injectable({ providedIn: 'root' })
export class AdminEarningsService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = `${environment.apiBaseUrl}/admin/earnings`;

  getEarnings(query: AdminEarningsQuery = {}): Observable<AdminEarningsResponse> {
    let params = new HttpParams();

    if (query.from) {
      params = params.set('from', query.from);
    }

    if (query.to) {
      params = params.set('to', query.to);
    }

    return this.http.get<AdminEarningsResponse>(this.baseUrl, { params });
  }
}
