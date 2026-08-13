import { Component, inject, OnInit, signal } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { PaymentInstructionsResponse } from '@contracts/payments';
import { firstValueFrom } from 'rxjs';
import { readApiError, readApiErrorCode } from '../../core/api/api-error.util';
import { BookingService } from '../../core/booking/booking.service';
import { readBookingErrorMessage } from '../booking-error.util';
import { APP_COPY } from '../../core/i18n/app-copy.constants';
import { PaymentInstructionsPanelComponent } from '../shared/payment-instructions-panel.component';
import { BookingStepIndicatorComponent } from '../shared/booking-step-indicator.component';

type PaymentInstructionsViewModel =
  | { status: 'loading' }
  | { status: 'error'; message: string }
  | { status: 'ready'; instructions: PaymentInstructionsResponse }
  | { status: 'submitted' };

@Component({
  selector: 'app-payment-instructions',
  imports: [PaymentInstructionsPanelComponent, BookingStepIndicatorComponent],
  templateUrl: './payment-instructions.component.html',
  styleUrl: './payment-instructions.component.scss',
})
export class PaymentInstructionsComponent implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly bookingService = inject(BookingService);

  protected readonly copy = APP_COPY;
  readonly viewModel = signal<PaymentInstructionsViewModel>({ status: 'loading' });
  readonly bookingId = signal<string | null>(null);

  ngOnInit(): void {
    void this.loadInstructions();
  }

  async reload(): Promise<void> {
    await this.loadInstructions();
  }

  goToDashboard(): void {
    void this.router.navigate(['/dashboard']);
  }

  onReceiptSubmitted(): void {
    this.viewModel.set({ status: 'submitted' });
  }

  private async loadInstructions(): Promise<void> {
    const bookingId = this.route.snapshot.paramMap.get('id');
    if (!bookingId) {
      this.viewModel.set({
        status: 'error',
        message: 'معرف الحجز غير صالح.',
      });
      return;
    }

    this.bookingId.set(bookingId);
    this.viewModel.set({ status: 'loading' });

    try {
      const instructions = await firstValueFrom(
        this.bookingService.getPaymentInstructions(bookingId),
      );
      this.viewModel.set({ status: 'ready', instructions });
    } catch (err) {
      const code = readApiErrorCode(err);
      this.viewModel.set({
        status: 'error',
        message: readBookingErrorMessage(
          code,
          readApiError(err, 'تعذر تحميل تعليمات الدفع. حاول مرة أخرى.'),
        ),
      });
    }
  }
}
