import { Component, inject, OnDestroy, OnInit } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { PaymentInstructionsResponse, PaymentMethod } from '@contracts/payments';
import { firstValueFrom } from 'rxjs';
import { readApiError, readApiErrorCode } from '../../core/api/api-error.util';
import { BookingService } from '../../core/booking/booking.service';
import { readBookingErrorMessage } from '../booking-error.util';

type PaymentInstructionsViewModel =
  | { status: 'loading' }
  | { status: 'error'; message: string }
  | { status: 'ready'; instructions: PaymentInstructionsResponse }
  | { status: 'submitted' };

@Component({
  selector: 'app-payment-instructions',
  imports: [FormsModule],
  templateUrl: './payment-instructions.component.html',
  styleUrl: './payment-instructions.component.scss',
})
export class PaymentInstructionsComponent implements OnInit, OnDestroy {
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly bookingService = inject(BookingService);

  viewModel: PaymentInstructionsViewModel = { status: 'loading' };
  countdownLabel = '';
  deadlineExpired = false;
  uploadMethod: PaymentMethod = 'VodafoneCash';
  senderReference = '';
  selectedFile: File | null = null;
  uploadError = '';
  uploading = false;

  private countdownTimer: ReturnType<typeof setInterval> | null = null;
  private deadlineUtc: string | null = null;
  private bookingId: string | null = null;

  ngOnInit(): void {
    void this.loadInstructions();
  }

  ngOnDestroy(): void {
    this.clearCountdown();
  }

  formatPrice(amount: number, currency: string): string {
    return new Intl.NumberFormat('ar-EG', {
      style: 'currency',
      currency,
      maximumFractionDigits: 0,
    }).format(amount);
  }

  async reload(): Promise<void> {
    await this.loadInstructions();
  }

  onFileSelected(event: Event): void {
    const input = event.target as HTMLInputElement;
    this.selectedFile = input.files?.[0] ?? null;
    this.uploadError = '';
  }

  async submitReceipt(): Promise<void> {
    if (!this.bookingId || this.uploading || this.deadlineExpired) {
      return;
    }

    if (!this.selectedFile) {
      this.uploadError = 'يرجى اختيار صورة الإيصال.';
      return;
    }

    this.uploading = true;
    this.uploadError = '';

    try {
      await firstValueFrom(
        this.bookingService.uploadReceipt(
          this.bookingId,
          this.selectedFile,
          this.uploadMethod,
          this.senderReference,
        ),
      );

      this.viewModel = { status: 'submitted' };
      this.clearCountdown();
    } catch (err) {
      const code = readApiErrorCode(err);
      this.uploadError = readBookingErrorMessage(
        code,
        readApiError(err, 'تعذر رفع الإيصال. حاول مرة أخرى.'),
      );
    } finally {
      this.uploading = false;
    }
  }

  goToDashboard(): void {
    void this.router.navigate(['/dashboard']);
  }

  private async loadInstructions(): Promise<void> {
    const bookingId = this.route.snapshot.paramMap.get('id');
    if (!bookingId) {
      this.viewModel = {
        status: 'error',
        message: 'معرّف الحجز غير صالح.',
      };
      return;
    }

    this.bookingId = bookingId;
    this.viewModel = { status: 'loading' };
    this.clearCountdown();

    try {
      const instructions = await firstValueFrom(
        this.bookingService.getPaymentInstructions(bookingId),
      );
      this.viewModel = { status: 'ready', instructions };
      this.deadlineUtc = instructions.receiptUploadDeadlineUtc;
      this.updateCountdown();
      this.countdownTimer = setInterval(() => this.updateCountdown(), 1000);
    } catch (err) {
      const code = readApiErrorCode(err);
      this.viewModel = {
        status: 'error',
        message: readBookingErrorMessage(
          code,
          readApiError(err, 'تعذر تحميل تعليمات الدفع. حاول مرة أخرى.'),
        ),
      };
    }
  }

  private updateCountdown(): void {
    if (!this.deadlineUtc) {
      return;
    }

    const remainingMs = new Date(this.deadlineUtc).getTime() - Date.now();
    if (remainingMs <= 0) {
      this.countdownLabel = 'انتهت مهلة رفع الإيصال';
      this.deadlineExpired = true;
      this.clearCountdown();
      return;
    }

    this.deadlineExpired = false;
    const totalSeconds = Math.floor(remainingMs / 1000);
    const hours = Math.floor(totalSeconds / 3600);
    const minutes = Math.floor((totalSeconds % 3600) / 60);
    const seconds = totalSeconds % 60;

    const formatter = new Intl.NumberFormat('ar-EG');
    const parts: string[] = [];

    if (hours > 0) {
      parts.push(`${formatter.format(hours)} س`);
    }

    parts.push(`${formatter.format(minutes)} د`);
    parts.push(`${formatter.format(seconds)} ث`);

    this.countdownLabel = parts.join(' ');
  }

  private clearCountdown(): void {
    if (this.countdownTimer !== null) {
      clearInterval(this.countdownTimer);
      this.countdownTimer = null;
    }
  }
}
