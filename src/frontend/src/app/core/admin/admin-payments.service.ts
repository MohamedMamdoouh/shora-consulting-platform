import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { PaymentRefundResponse, RecordRefundRequest } from '@contracts/payments';
import { environment } from '../../../environments/environment';

@Injectable({ providedIn: 'root' })
export class AdminPaymentsService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = `${environment.apiBaseUrl}/admin/payments`;

  recordRefund(paymentId: string, request: RecordRefundRequest): Observable<PaymentRefundResponse> {
    return this.http.post<PaymentRefundResponse>(
      `${this.baseUrl}/${paymentId}/refunds/record`,
      request,
    );
  }
}
