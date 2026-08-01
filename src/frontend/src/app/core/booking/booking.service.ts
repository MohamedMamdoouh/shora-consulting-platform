import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { CreateBookingRequest, ReserveBookingResponse } from '@contracts/booking';
import { PaymentInstructionsResponse, PaymentMethod, UploadReceiptResponse } from '@contracts/payments';
import { environment } from '../../../environments/environment';
import { buildReceiptUploadFormData } from './receipt-upload-form-data.util';

@Injectable({ providedIn: 'root' })
export class BookingService {
  private readonly http = inject(HttpClient);

  reserve(request: CreateBookingRequest): Observable<ReserveBookingResponse> {
    return this.http.post<ReserveBookingResponse>(`${environment.apiBaseUrl}/bookings`, request);
  }

  getPaymentInstructions(bookingId: string): Observable<PaymentInstructionsResponse> {
    return this.http.get<PaymentInstructionsResponse>(
      `${environment.apiBaseUrl}/bookings/${bookingId}/payment-instructions`,
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