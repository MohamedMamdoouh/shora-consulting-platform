import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import {
  AdminBookingsQuery,
  AdminBookingsResponse,
  AdminBookingCancellationResponse,
  DeclineCancellationRequestBody,
} from '@contracts/booking';
import {
  AdminBookingReceiptsResponse,
  AdminReceiptDecisionResponse,
  DeclineReceiptRequest,
} from '@contracts/payments';
import { environment } from '../../../environments/environment';

@Injectable({ providedIn: 'root' })
export class AdminBookingsService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = `${environment.apiBaseUrl}/admin/bookings`;

  listBookings(query: AdminBookingsQuery = {}): Observable<AdminBookingsResponse> {
    let params = new HttpParams();

    if (query.status) {
      params = params.set('status', query.status);
    }

    if (query.from) {
      params = params.set('from', query.from);
    }

    if (query.to) {
      params = params.set('to', query.to);
    }

    if (query.page !== undefined) {
      params = params.set('page', query.page.toString());
    }

    if (query.pageSize !== undefined) {
      params = params.set('pageSize', query.pageSize.toString());
    }

    return this.http.get<AdminBookingsResponse>(this.baseUrl, { params });
  }

  getReceipts(bookingId: string): Observable<AdminBookingReceiptsResponse> {
    return this.http.get<AdminBookingReceiptsResponse>(`${this.baseUrl}/${bookingId}/receipts`);
  }

  approveReceipt(bookingId: string): Observable<AdminReceiptDecisionResponse> {
    return this.http.post<AdminReceiptDecisionResponse>(
      `${this.baseUrl}/${bookingId}/receipts/approve`,
      null,
    );
  }

  declineReceipt(
    bookingId: string,
    request: DeclineReceiptRequest,
  ): Observable<AdminReceiptDecisionResponse> {
    return this.http.post<AdminReceiptDecisionResponse>(
      `${this.baseUrl}/${bookingId}/receipts/decline`,
      request,
    );
  }

  cancelBooking(bookingId: string): Observable<AdminBookingCancellationResponse> {
    return this.http.post<AdminBookingCancellationResponse>(
      `${this.baseUrl}/${bookingId}/cancel`,
      null,
    );
  }

  approveCancellationRequest(bookingId: string): Observable<AdminBookingCancellationResponse> {
    return this.http.post<AdminBookingCancellationResponse>(
      `${this.baseUrl}/${bookingId}/cancellation-requests/approve`,
      null,
    );
  }

  declineCancellationRequest(
    bookingId: string,
    request: DeclineCancellationRequestBody,
  ): Observable<AdminBookingCancellationResponse> {
    return this.http.post<AdminBookingCancellationResponse>(
      `${this.baseUrl}/${bookingId}/cancellation-requests/decline`,
      request,
    );
  }
}
