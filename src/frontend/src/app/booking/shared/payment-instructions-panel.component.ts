import {
  Component,
  EventEmitter,
  Input,
  OnChanges,
  OnDestroy,
  Output,
  SimpleChanges,
  inject,
  signal,
} from '@angular/core';
import { FormsModule } from '@angular/forms';
import { PaymentInstructionsResponse, PaymentMethod } from '@contracts/payments';
import { firstValueFrom } from 'rxjs';
import { readApiError, readApiErrorCode } from '../../core/api/api-error.util';
import { BookingService } from '../../core/booking/booking.service';
import { formatCurrency, formatNumber } from '../../core/i18n/app-locale';
import { formatReceiptDeclineReasonCode } from '../../core/i18n/receipt-decline-labels.util';
import { readBookingErrorMessage } from '../booking-error.util';

@Component({
  selector: 'app-payment-instructions-panel',
  imports: [FormsModule],
  templateUrl: './payment-instructions-panel.component.html',
  styleUrl: './payment-instructions-panel.component.scss',
})
export class PaymentInstructionsPanelComponent implements OnChanges, OnDestroy {
  private readonly bookingService = inject(BookingService);

  @Input({ required: true }) bookingId!: string;
  @Input({ required: true }) instructions!: PaymentInstructionsResponse;
  @Input() declineReason?: string | null;

  @Output() readonly receiptSubmitted = new EventEmitter<void>();

  readonly countdownLabel = signal('');
  readonly deadlineExpired = signal(false);
  uploadMethod: PaymentMethod = 'VodafoneCash';
  senderReference = '';
  readonly selectedFile = signal<File | null>(null);
  readonly fileTouched = signal(false);
  readonly uploadError = signal('');
  readonly uploading = signal(false);

  private countdownTimer: ReturnType<typeof setInterval> | null = null;

  ngOnChanges(changes: SimpleChanges): void {
    if (changes['instructions']) {
      this.restartCountdown();
    }
  }

  ngOnDestroy(): void {
    this.clearCountdown();
  }

  readonly formatPrice = formatCurrency;
  readonly formatDeclineReason = formatReceiptDeclineReasonCode;

  onFileSelected(event: Event): void {
    const input = event.target as HTMLInputElement;
    this.selectedFile.set(input.files?.[0] ?? null);
    this.fileTouched.set(true);
    this.uploadError.set('');
  }

  markFileFieldTouched(): void {
    this.fileTouched.set(true);
  }

  async submitReceipt(): Promise<void> {
    const file = this.selectedFile();
    if (this.uploading() || this.deadlineExpired() || !file) {
      if (!file && !this.uploading() && !this.deadlineExpired()) {
        this.fileTouched.set(true);
        this.uploadError.set('يرجى اختيار صورة الإيصال.');
      }
      return;
    }

    this.uploading.set(true);
    this.uploadError.set('');

    try {
      await firstValueFrom(
        this.bookingService.uploadReceipt(
          this.bookingId,
          file,
          this.uploadMethod,
          this.senderReference,
        ),
      );

      this.clearCountdown();
      this.receiptSubmitted.emit();
    } catch (err) {
      const code = readApiErrorCode(err);
      this.uploadError.set(
        readBookingErrorMessage(
          code,
          readApiError(err, 'تعذر رفع الإيصال. حاول مرة أخرى.'),
        ),
      );
    } finally {
      this.uploading.set(false);
    }
  }

  private restartCountdown(): void {
    this.clearCountdown();
    this.deadlineExpired.set(false);
    this.updateCountdown();
    this.countdownTimer = setInterval(() => this.updateCountdown(), 1000);
  }

  private updateCountdown(): void {
    const remainingMs = new Date(this.instructions.receiptUploadDeadlineUtc).getTime() - Date.now();

    if (remainingMs <= 0) {
      this.countdownLabel.set('انتهت مهلة رفع الإيصال');
      this.deadlineExpired.set(true);
      this.clearCountdown();
      return;
    }

    this.deadlineExpired.set(false);
    const totalSeconds = Math.floor(remainingMs / 1000);
    const hours = Math.floor(totalSeconds / 3600);
    const minutes = Math.floor((totalSeconds % 3600) / 60);
    const seconds = totalSeconds % 60;

    const parts: string[] = [];

    if (hours > 0) {
      parts.push(`${formatNumber(hours)} ساعة`);
    }

    parts.push(`${formatNumber(minutes)} دقيقة`);
    parts.push(`${formatNumber(seconds)} ثانية`);

    this.countdownLabel.set(parts.join(' '));
  }

  private clearCountdown(): void {
    if (this.countdownTimer !== null) {
      clearInterval(this.countdownTimer);
      this.countdownTimer = null;
    }
  }
}
