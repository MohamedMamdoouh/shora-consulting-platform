import { Component, EventEmitter, Input, Output, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule } from '@angular/forms';
import { AdminBookingListItem, CancellationDecisionReasonCode } from '@contracts/booking';
import { firstValueFrom } from 'rxjs';
import { readApiError } from '../../core/api/api-error.util';
import { AdminBookingsService } from '../../core/admin/admin-bookings.service';
import { ConfirmDialogService } from '../../core/ui/confirm-dialog.service';
import { APP_COPY } from '../../core/i18n/app-copy.constants';
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
export class AdminCancellationReviewPanelComponent {
  private readonly fb = inject(FormBuilder);
  private readonly adminBookingsService = inject(AdminBookingsService);
  private readonly confirmDialog = inject(ConfirmDialogService);

  protected readonly copy = APP_COPY;

  @Input({ required: true }) item!: AdminBookingListItem;

  @Output() readonly closed = new EventEmitter<void>();
  @Output() readonly changed = new EventEmitter<void>();

  readonly isApproving = signal(false);
  readonly isDeclining = signal(false);
  readonly showDeclineForm = signal(false);

  readonly declineReasonOptions = CANCELLATION_DECISION_REASON_OPTIONS;
  readonly formatRequestedAt = formatRequestedAt;
  readonly formatRemainingTime = formatRemainingTime;

  readonly declineForm = this.fb.nonNullable.group({
    reasonCode: this.fb.nonNullable.control<CancellationDecisionReasonCode>('Policy'),
    reasonNote: this.fb.control<string | null>(null),
  });

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
      confirmLabel: this.copy.admin.dialog.approveCancellationAction,
      variant: 'danger',
    });

    if (!confirmed) {
      return;
    }

    this.isApproving.set(true);

    try {
      await firstValueFrom(
        this.adminBookingsService.approveCancellationRequest(this.item.bookingId),
      );
      await this.confirmDialog.result({
        message: 'تمت الموافقة على طلب الإلغاء.',
        onComplete: () => {
          this.changed.emit();
          this.close();
        },
      });
    } catch (error) {
      await this.confirmDialog.result({
        message: readApiError(error, 'تعذر الموافقة على طلب الإلغاء. حاول مرة أخرى.'),
        variant: 'danger',
      });
    } finally {
      this.isApproving.set(false);
    }
  }

  openDeclineForm(): void {
    this.showDeclineForm.set(true);
    this.declineForm.reset({
      reasonCode: 'Policy',
      reasonNote: null,
    });
  }

  cancelDeclineForm(): void {
    this.showDeclineForm.set(false);
  }

  async submitDecline(): Promise<void> {
    if (this.isApproving() || this.isDeclining()) {
      return;
    }

    const values = this.declineForm.getRawValue();

    const confirmed = await this.confirmDialog.confirm({
      title: this.copy.admin.dialog.declineCancellationTitle,
      message: this.copy.admin.dialog.declineCancellationMessage,
      confirmLabel: this.copy.admin.dialog.declineCancellationAction,
      variant: 'danger',
    });

    if (!confirmed) {
      return;
    }

    this.isDeclining.set(true);

    try {
      await firstValueFrom(
        this.adminBookingsService.declineCancellationRequest(this.item.bookingId, {
          reasonCode: values.reasonCode,
          reasonNote: values.reasonNote?.trim() || null,
        }),
      );
      await this.confirmDialog.result({
        message: 'تم رفض طلب الإلغاء.',
        onComplete: () => {
          this.changed.emit();
          this.close();
        },
      });
    } catch (error) {
      await this.confirmDialog.result({
        message: readApiError(error, 'تعذر رفض طلب الإلغاء. حاول مرة أخرى.'),
        variant: 'danger',
      });
    } finally {
      this.isDeclining.set(false);
    }
  }
}
