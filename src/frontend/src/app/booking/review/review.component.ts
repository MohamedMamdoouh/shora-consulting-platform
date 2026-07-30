import { Component, inject, OnInit } from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import { CreateBookingRequest, DeliveryMethod } from '@contracts/booking';
import { firstValueFrom } from 'rxjs';
import { environment } from '../../../environments/environment';
import { ApiCacheService } from '../../core/api/api-cache.service';
import { readApiError, readApiErrorCode } from '../../core/api/api-error.util';
import { AuthService } from '../../core/auth/auth.service';
import { BookingService } from '../../core/booking/booking.service';
import { isSlotUnavailableError, readBookingErrorMessage } from '../booking-error.util';
import { BookingFlowStateService } from '../booking-flow-state.service';
import { formatSlotSummary } from '../utils/slot-grouping.util';

@Component({
  selector: 'app-booking-review',
  imports: [RouterLink],
  templateUrl: './review.component.html',
  styleUrl: './review.component.scss',
})
export class ReviewComponent implements OnInit {
  private readonly auth = inject(AuthService);
  private readonly bookingFlow = inject(BookingFlowStateService);
  private readonly bookingService = inject(BookingService);
  private readonly apiCache = inject(ApiCacheService);
  private readonly router = inject(Router);

  readonly flow = this.bookingFlow.getState()!;
  readonly slotSummary = formatSlotSummary(this.flow.slotStartUtc);
  readonly deliveryLabel = this.formatDeliveryMethod(this.flow.deliveryMethod!);

  errorMessage = '';
  infoMessage = '';
  slotUnavailable = false;
  isSubmitting = false;
  isResending = false;

  ngOnInit(): void {
    if (!this.auth.isAuthenticated()) {
      void this.router.navigate(['/auth/login'], {
        queryParams: { returnUrl: '/booking/review' },
      });
    }
  }

  get isLoggedIn(): boolean {
    return this.auth.isAuthenticated();
  }

  get isVerified(): boolean {
    return this.auth.getCurrentUser()?.emailConfirmed === true;
  }

  get userEmail(): string | null {
    return this.auth.getUserEmail();
  }

  async resendVerification(): Promise<void> {
    const email = this.userEmail;
    if (!email || this.isResending) {
      return;
    }

    this.errorMessage = '';
    this.infoMessage = '';
    this.isResending = true;

    try {
      await firstValueFrom(this.auth.resendVerification(email));
      this.infoMessage = 'إذا كان الحساب غير مؤكد، فقد أُرسل رابط التحقق إلى بريدك.';
    } catch (err) {
      this.errorMessage = readApiError(err, 'تعذر إرسال رابط التحقق. حاول مرة أخرى.');
    } finally {
      this.isResending = false;
    }
  }

  async reserve(): Promise<void> {
    if (this.isSubmitting || !this.isLoggedIn || !this.isVerified) {
      return;
    }

    const { slotId, deliveryMethod, contactPhone } = this.flow;
    if (!deliveryMethod) {
      return;
    }

    this.errorMessage = '';
    this.infoMessage = '';
    this.slotUnavailable = false;
    this.isSubmitting = true;

    this.apiCache.invalidateUrlPrefix(`${environment.apiBaseUrl}/availability`);

    const request: CreateBookingRequest = {
      availabilitySlotId: slotId,
      deliveryMethod,
      contactPhone: deliveryMethod === 'VoiceCall' ? contactPhone : null,
    };

    try {
      const response = await firstValueFrom(this.bookingService.reserve(request));
      this.bookingFlow.clear();
      await this.router.navigate(['/booking/payment', response.bookingId]);
    } catch (err) {
      const code = readApiErrorCode(err);
      this.errorMessage = readBookingErrorMessage(
        code,
        readApiError(err, 'تعذر إتمام الحجز. حاول مرة أخرى.'),
      );
      this.slotUnavailable = isSlotUnavailableError(code);
    } finally {
      this.isSubmitting = false;
    }
  }

  private formatDeliveryMethod(method: DeliveryMethod): string {
    return method === 'VoiceCall' ? 'مكالمة صوتية' : 'محادثة واتساب';
  }
}
