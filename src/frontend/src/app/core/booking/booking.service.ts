import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { CreateBookingRequest, ReserveBookingResponse } from '@contracts/booking';
import { PaymentInstructionsResponse } from '@contracts/payments';
import { environment } from '../../../environments/environment';

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
}