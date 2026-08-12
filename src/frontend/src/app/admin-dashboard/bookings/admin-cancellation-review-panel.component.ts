import {
  Component,
  EventEmitter,
  Input,
  OnDestroy,
  OnInit,
  Output,
  inject,
} from '@angular/core';
import { FormBuilder, ReactiveFormsModule } from '@angular/forms';
import { AdminBookingListItem, CancellationDecisionReasonCode } from '@contracts/booking';
import { firstValueFrom } from 'rxjs';
import { readApiError, readApiErrorCode } from '../../core/api/api-error.util';
import { AdminBookingsService } from '../../core/admin/admin-bookings.service';
import { readBookingErrorMessage } from '../../booking/booking-error.util';
import {
  CANCELLATION_DECISION_REASON_OPTIONS,
  formatRemainingTime,
  formatRequestedAt,
} from './admin-cancellation-labels.util';

@Component({
  selector: 'app-admin-cancellation-review-panel',
  imports: [ReactiveFormsModule],
  templateUrl: './admin-cancellation-review-panel.component.html',
  styleUrl: './admin-cancellation-review-panel.component.scss',
})
export class AdminCancellationReviewPanelComponent implements OnInit, OnDestroy {
  private readonly fb = inject(FormBuilder);
  private readonly adminBookingsService = inject(AdminBookingsService);

  @Input({ required: true }) item!: AdminBookingListItem;

  @Output() readonly closed = new EventEmitter<void>();
  @Output() readonly changed = new EventEmitter<void>();

  actionError = '';
  isApproving = false;
  isDeclining = false;
  showDeclineForm = false;
  countdownLabel = '';

  readonly declineReasonOptions = CANCELLATION_DECISION_REASON_OPTIONS;
  readonly formatRequestedAt = formatRequestedAt;

  readonly declineForm = this.fb.nonNullable.group({
    reasonCode: this.fb.nonNullable.control<CancellationDecisionReasonCode>('Policy'),
    reasonNote: this.fb.control<string | null>(null),
  });

  private countdownTimer: ReturnType<typeof setInterval> | null = null;

  ngOnInit(): void {
    this.updateCountdown();
    this.countdownTimer = setInterval(() => this.updateCountdown(), 1000);
  }

  ngOnDestroy(): void {
    if (this.countdownTimer !== null) {
      clearInterval(this.countdownTimer);
      this.countdownTimer = null;
    }
  }

  close(): void {
    this.closed.emit();
  }

  async approveRequest(): Promise<void> {
    if (this.isApproving || this.isDeclining) {
      return;
    }

    const refundNote =
      this.item.paymentStatus === 'Approved'
        ? ' سيتم وضع علامة «استرداد مستحق» لأن الدفع مقبول.'
        : '';

    const confirmed = window.confirm(
      `هل تريد الموافقة على إلغاء حجز ${this.item.clientDisplayName}؟${refundNote}`,
    );

    if (!confirmed) {
      return;
    }

    this.isApproving = true;
    this.actionError = '';

    try {
      await firstValueFrom(this.adminBookingsService.approveCancellationRequest(this.item.bookingId));
      this.changed.emit();
      this.close();
    } catch (error) {
      this.actionError = readBookingErrorMessage(
        readApiErrorCode(error),
        readApiError(error, 'تعذر الموافقة على طلب الإلغاء. حاول مرة أخرى.'),
      );
    } finally {
      this.isApproving = false;
    }
  }

  openDeclineForm(): void {
    this.showDeclineForm = true;
    this.actionError = '';
    this.declineForm.reset({
      reasonCode: 'Policy',
      reasonNote: null,
    });
  }

  cancelDeclineForm(): void {
    this.showDeclineForm = false;
    this.actionError = '';
  }

  async submitDecline(): Promise<void> {
    if (this.isApproving || this.isDeclining) {
      return;
    }

    const values = this.declineForm.getRawValue();
    this.isDeclining = true;
    this.actionError = '';

    try {
      await firstValueFrom(
        this.adminBookingsService.declineCancellationRequest(this.item.bookingId, {
          reasonCode: values.reasonCode,
          reasonNote: values.reasonNote?.trim() || null,
        }),
      );
      this.changed.emit();
      this.close();
    } catch (error) {
      this.actionError = readBookingErrorMessage(
        readApiErrorCode(error),
        readApiError(error, 'تعذر رفض طلب الإلغاء. حاول مرة أخرى.'),
      );
    } finally {
      this.isDeclining = false;
    }
  }

  private updateCountdown(): void {
    const deadline = this.item.cancellationRequest?.autoDeclineAtUtc;

    if (!deadline) {
      this.countdownLabel = '—';
      return;
    }

    this.countdownLabel = formatRemainingTime(deadline);
  }
}
