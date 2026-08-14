import { Component, EventEmitter, Input, OnInit, Output, inject, signal } from '@angular/core';

import { FormBuilder, ReactiveFormsModule } from '@angular/forms';

import {
  AdminBookingReceiptsResponse,
  AdminPaymentReceiptItem,
  ReceiptDeclineReasonCode,
} from '@contracts/payments';

import { firstValueFrom } from 'rxjs';

import { readApiError } from '../../core/api/api-error.util';

import { AdminBookingsService } from '../../core/admin/admin-bookings.service';

import { ConfirmDialogService } from '../../core/ui/confirm-dialog.service';

import { APP_COPY } from '../../core/i18n/app-copy.constants';

import {
  RECEIPT_DECLINE_REASON_OPTIONS,
  findPendingReceipt,
  formatMalwareScanStatus,
  formatMoney,
  formatPaymentMethod,
  formatPaymentStatus,
  formatReceiptDeclineReasonCode,
  formatReceiptReviewStatus,
  formatReceiptUploadedAt,
  formatReviewWarning,
} from './admin-receipt-labels.util';

type PanelState =
  | { status: 'loading' }
  | { status: 'error'; message: string }
  | {
      status: 'ready';
      data: AdminBookingReceiptsResponse;
      pendingReceipt?: AdminPaymentReceiptItem;
    };

@Component({
  selector: 'app-admin-receipt-review-panel',

  imports: [ReactiveFormsModule],

  templateUrl: './admin-receipt-review-panel.component.html',

  styleUrl: './admin-receipt-review-panel.component.scss',
})
export class AdminReceiptReviewPanelComponent implements OnInit {
  private readonly fb = inject(FormBuilder);

  private readonly adminBookingsService = inject(AdminBookingsService);

  private readonly confirmDialog = inject(ConfirmDialogService);

  protected readonly copy = APP_COPY;

  @Input({ required: true }) bookingId!: string;

  @Input({ required: true }) clientDisplayName!: string;

  @Output() readonly closed = new EventEmitter<void>();

  @Output() readonly changed = new EventEmitter<void>();

  readonly panelState = signal<PanelState>({ status: 'loading' });

  readonly isApproving = signal(false);

  readonly isDeclining = signal(false);

  readonly showDeclineForm = signal(false);

  readonly declineReasonOptions = RECEIPT_DECLINE_REASON_OPTIONS;

  readonly formatPaymentMethod = formatPaymentMethod;

  readonly formatPaymentStatus = formatPaymentStatus;

  readonly formatReceiptReviewStatus = formatReceiptReviewStatus;

  readonly formatMalwareScanStatus = formatMalwareScanStatus;

  readonly formatReceiptDeclineReasonCode = formatReceiptDeclineReasonCode;

  readonly formatReceiptUploadedAt = formatReceiptUploadedAt;

  readonly formatReviewWarning = formatReviewWarning;

  readonly formatMoney = formatMoney;

  readonly declineForm = this.fb.nonNullable.group({
    reasonCode: this.fb.nonNullable.control<ReceiptDeclineReasonCode>('UnreadableImage'),

    reasonNote: this.fb.control<string | null>(null),
  });

  ngOnInit(): void {
    void this.loadReceipts();
  }

  close(): void {
    this.closed.emit();
  }

  async loadReceipts(): Promise<void> {
    this.panelState.set({ status: 'loading' });

    try {
      const data = await firstValueFrom(this.adminBookingsService.getReceipts(this.bookingId));

      const pendingReceipt = findPendingReceipt(data.receipts);

      this.panelState.set({ status: 'ready', data, pendingReceipt });

      this.showDeclineForm.set(false);
    } catch (error) {
      this.panelState.set({
        status: 'error',

        message: readApiError(error, 'تعذر تحميل الإيصالات. حاول مرة أخرى.'),
      });
    }
  }

  async approveReceipt(): Promise<void> {
    if (this.isApproving() || this.isDeclining()) {
      return;
    }

    const confirmed = await this.confirmDialog.confirm({
      title: this.copy.admin.dialog.approveReceiptTitle,

      message: this.copy.admin.dialog.approveReceiptMessage,

      confirmLabel: this.copy.admin.dialog.approveReceiptAction,
    });

    if (!confirmed) {
      return;
    }

    this.isApproving.set(true);

    try {
      await firstValueFrom(this.adminBookingsService.approveReceipt(this.bookingId));

      await this.confirmDialog.result({
        message: 'تم قبول الإيصال وتأكيد الحجز.',

        onComplete: () => {
          this.changed.emit();

          this.close();
        },
      });
    } catch (error) {
      await this.confirmDialog.result({
        message: readApiError(error, 'تعذر قبول الإيصال. حاول مرة أخرى.'),

        variant: 'danger',
      });
    } finally {
      this.isApproving.set(false);
    }
  }

  openDeclineForm(): void {
    this.showDeclineForm.set(true);

    this.declineForm.reset({
      reasonCode: 'UnreadableImage',

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

    const confirmed = await this.confirmDialog.confirm({
      title: this.copy.admin.dialog.declineReceiptTitle,

      message: this.copy.admin.dialog.declineReceiptMessage,

      confirmLabel: this.copy.admin.dialog.declineReceiptAction,

      variant: 'danger',
    });

    if (!confirmed) {
      return;
    }

    const values = this.declineForm.getRawValue();

    this.isDeclining.set(true);

    try {
      await firstValueFrom(
        this.adminBookingsService.declineReceipt(this.bookingId, {
          reasonCode: values.reasonCode,

          reasonNote: values.reasonNote?.trim() || null,
        }),
      );

      await this.confirmDialog.result({
        message: 'تم رفض الإيصال وإعادة فتح مهلة الرفع للعميل.',

        onComplete: () => {
          this.changed.emit();

          this.close();
        },
      });
    } catch (error) {
      await this.confirmDialog.result({
        message: readApiError(error, 'تعذر رفض الإيصال. حاول مرة أخرى.'),

        variant: 'danger',
      });
    } finally {
      this.isDeclining.set(false);
    }
  }
}
