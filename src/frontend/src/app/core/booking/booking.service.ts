import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import {
  CancellationRequestBody,
  CancellationRequestResponse,
  CreateBookingRequest,
  MyBookingsQuery,
  MyBookingsResponse,
  ReserveBookingResponse,
} from '@contracts/booking';
import { PaymentInstructionsResponse, PaymentMethod, UploadReceiptResponse } from '@contracts/payments';
import { environment } from '../../../environments/environment';
import { buildReceiptUploadFormData } from './receipt-upload-form-data.util';

@Injectable({ providedIn: 'root' })
export class BookingService {
  private readonly http = inject(HttpClient);

  reserve(request: CreateBookingRequest): Observable<ReserveBookingResponse> {
    return this.http.post<ReserveBookingResponse>(`${environment.apiBaseUrl}/bookings`, request);
  }

  getMyBookings(query: MyBookingsQuery = {}): Observable<MyBookingsResponse> {
    let params = new HttpParams();

    if (query.status) {
      params = params.set('status', query.status);
    }

    if (query.page !== undefined) {
      params = params.set('page', query.page.toString());
    }

    if (query.pageSize !== undefined) {
      params = params.set('pageSize', query.pageSize.toString());
    }

    return this.http.get<MyBookingsResponse>(`${environment.apiBaseUrl}/bookings/mine`, { params });
  }

  getPaymentInstructions(bookingId: string): Observable<PaymentInstructionsResponse> {
    return this.http.get<PaymentInstructionsResponse>(
      `${environment.apiBaseUrl}/bookings/${bookingId}/payment-instructions`,
    );
  }

  cancelHold(bookingId: string): Observable<void> {
    return this.http.post<void>(`${environment.apiBaseUrl}/bookings/${bookingId}/cancel`, null);
  }

  requestCancellation(
    bookingId: string,
    body: CancellationRequestBody = {},
  ): Observable<CancellationRequestResponse> {
    return this.http.post<CancellationRequestResponse>(
      `${environment.apiBaseUrl}/bookings/${bookingId}/cancellation-requests`,
      body,
    );
  }

  markCancellationDecisionSeen(bookingId: string): Observable<void> {
    return this.http.post<void>(
      `${environment.apiBaseUrl}/bookings/${bookingId}/cancellation-requests/decision-seen`,
      null,
    );
  }

  uploadReceipt(
    bookingId: string,
    image: File,
    method: PaymentMethod,
    senderReference?: string | null,
  ): Observable<UploadReceiptResponse> {
    return this.http.post<UploadReceiptResponse>(
      `${environment.apiBaseUrl}/payments/${bookingId}/receipt`,
      buildReceiptUploadFormData(image, method, senderReference),
    );
  }
}