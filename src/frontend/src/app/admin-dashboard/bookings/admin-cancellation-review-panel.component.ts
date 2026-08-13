import {
  Component,
  EventEmitter,
  Input,
  OnDestroy,
  OnInit,
  Output,
  inject,
  signal,
} from '@angular/core';
import { FormBuilder, ReactiveFormsModule } from '@angular/forms';
import { AdminBookingListItem, CancellationDecisionReasonCode } from '@contracts/booking';
import { firstValueFrom } from 'rxjs';
import { readApiError, readApiErrorCode } from '../../core/api/api-error.util';
import { AdminBookingsService } from '../../core/admin/admin-bookings.service';
import { ConfirmDialogService } from '../../core/ui/confirm-dialog.service';
import { APP_COPY } from '../../core/i18n/app-copy.constants';
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
  private readonly confirmDialog = inject(ConfirmDialogService);

  protected readonly copy = APP_COPY;

  @Input({ required: true }) item!: AdminBookingListItem;

  @Output() readonly closed = new EventEmitter<void>();
  @Output() readonly changed = new EventEmitter<void>();

  readonly actionError = signal('');
  readonly isApproving = signal(false);
  readonly isDeclining = signal(false);
  readonly showDeclineForm = signal(false);
  readonly countdownLabel = signal('');

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
    if (this.isApproving() || this.isDeclining()) {
      return;
    }

    const refundNote =
      this.item.paymentStatus === 'Approved' ? this.copy.admin.dialog.refundDueNote : '';

    const confirmed = await this.confirmDialog.confirm({
      title: this.copy.admin.dialog.approveCancellationTitle,
      message: this.copy.admin.dialog.approveCancellationMessage(refundNote),
      detail: this.copy.admin.customerName(this.item.clientDisplayName),
      confirmLabel: this.copy.admin.dialog.approveCancellationAction,
      variant: 'danger',
    });

    if (!confirmed) {
      return;
    }

    this.isApproving.set(true);
    this.actionError.set('');

    try {
      await firstValueFrom(this.adminBookingsService.approveCancellationRequest(this.item.bookingId));
      this.changed.emit();
      this.close();
    } catch (error) {
      this.actionError.set(
        readBookingErrorMessage(
          readApiErrorCode(error),
          readApiError(error, 'تعذر الموافقة على طلب الإلغاء. حاول مرة أخرى.'),
        ),
      );
    } finally {
      this.isApproving.set(false);
    }
  }

  openDeclineForm(): void {
    this.showDeclineForm.set(true);
    this.actionError.set('');
    this.declineForm.reset({
      reasonCode: 'Policy',
      reasonNote: null,
    });
  }

  cancelDeclineForm(): void {
    this.showDeclineForm.set(false);
    this.actionError.set('');
  }

  async submitDecline(): Promise<void> {
    if (this.isApproving() || this.isDeclining()) {
      return;
    }

    const values = this.declineForm.getRawValue();
    this.isDeclining.set(true);
    this.actionError.set('');

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
      this.actionError.set(
        readBookingErrorMessage(
          readApiErrorCode(error),
          readApiError(error, 'تعذر رفض طلب الإلغاء. حاول مرة أخرى.'),
        ),
      );
    } finally {
      this.isDeclining.set(false);
    }
  }

  private updateCountdown(): void {
    const deadline = this.item.cancellationRequest?.autoDeclineAtUtc;

    if (!deadline) {
      this.countdownLabel.set('—');
      return;
    }

    this.countdownLabel.set(formatRemainingTime(deadline));
  }
}
