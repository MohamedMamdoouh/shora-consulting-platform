import {
  Component,
  EventEmitter,
  Input,
  OnChanges,
  OnDestroy,
  Output,
  SimpleChanges,
  inject,
} from '@angular/core';
import { FormsModule } from '@angular/forms';
import { PaymentInstructionsResponse, PaymentMethod } from '@contracts/payments';
import { firstValueFrom } from 'rxjs';
import { readApiError, readApiErrorCode } from '../../core/api/api-error.util';
import { BookingService } from '../../core/booking/booking.service';
import { formatCurrency, formatNumber } from '../../core/i18n/app-locale';
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

  countdownLabel = '';
  deadlineExpired = false;
  uploadMethod: PaymentMethod = 'VodafoneCash';
  senderReference = '';
  selectedFile: File | null = null;
  uploadError = '';
  uploading = false;

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

  onFileSelected(event: Event): void {
    const input = event.target as HTMLInputElement;
    this.selectedFile = input.files?.[0] ?? null;
    this.uploadError = '';
  }

  async submitReceipt(): Promise<void> {
    if (this.uploading || this.deadlineExpired) {
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

      this.clearCountdown();
      this.receiptSubmitted.emit();
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

  private restartCountdown(): void {
    this.clearCountdown();
    this.deadlineExpired = false;
    this.updateCountdown();
    this.countdownTimer = setInterval(() => this.updateCountdown(), 1000);
  }

  private updateCountdown(): void {
    const remainingMs = new Date(this.instructions.receiptUploadDeadlineUtc).getTime() - Date.now();

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

    const parts: string[] = [];

    if (hours > 0) {
      parts.push(`${formatNumber(hours)} ساعة`);
    }

    parts.push(`${formatNumber(minutes)} دقيقة`);
    parts.push(`${formatNumber(seconds)} ثانية`);

    this.countdownLabel = parts.join(' ');
  }

  private clearCountdown(): void {
    if (this.countdownTimer !== null) {
      clearInterval(this.countdownTimer);
      this.countdownTimer = null;
    }
  }
}
