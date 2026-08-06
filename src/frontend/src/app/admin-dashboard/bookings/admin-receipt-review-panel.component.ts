import { Component, EventEmitter, Input, OnInit, Output, inject } from '@angular/core';
import { FormBuilder, ReactiveFormsModule } from '@angular/forms';
import {
  AdminBookingReceiptsResponse,
  AdminPaymentReceiptItem,
  ReceiptDeclineReasonCode,
} from '@contracts/payments';
import { firstValueFrom } from 'rxjs';
import { readApiError, readApiErrorCode } from '../../core/api/api-error.util';
import { AdminBookingsService } from '../../core/admin/admin-bookings.service';
import { readBookingErrorMessage } from '../../booking/booking-error.util';
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
  | { status: 'ready'; data: AdminBookingReceiptsResponse; pendingReceipt?: AdminPaymentReceiptItem };

@Component({
  selector: 'app-admin-receipt-review-panel',
  imports: [ReactiveFormsModule],
  templateUrl: './admin-receipt-review-panel.component.html',
  styleUrl: './admin-receipt-review-panel.component.scss',
})
export class AdminReceiptReviewPanelComponent implements OnInit {
  private readonly fb = inject(FormBuilder);
  private readonly adminBookingsService = inject(AdminBookingsService);

  @Input({ required: true }) bookingId!: string;
  @Input({ required: true }) clientDisplayName!: string;

  @Output() readonly closed = new EventEmitter<void>();
  @Output() readonly changed = new EventEmitter<void>();

  panelState: PanelState = { status: 'loading' };
  actionError = '';
  successMessage = '';
  isApproving = false;
  isDeclining = false;
  showDeclineForm = false;

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
    this.panelState = { status: 'loading' };
    this.actionError = '';

    try {
      const data = await firstValueFrom(this.adminBookingsService.getReceipts(this.bookingId));
      const pendingReceipt = findPendingReceipt(data.receipts);

      this.panelState = { status: 'ready', data, pendingReceipt };
      this.showDeclineForm = false;
    } catch (error) {
      this.panelState = {
        status: 'error',
        message: readApiError(error, 'تعذّر تحميل الإيصالات. حاول مرة أخرى.'),
      };
    }
  }

  async approveReceipt(): Promise<void> {
    if (this.isApproving || this.isDeclining) {
      return;
    }

    const confirmed = window.confirm(
      `هل تريد قبول إيصال الدفع وتأكيد حجز ${this.clientDisplayName}؟`,
    );

    if (!confirmed) {
      return;
    }

    this.isApproving = true;
    this.actionError = '';
    this.successMessage = '';

    try {
      await firstValueFrom(this.adminBookingsService.approveReceipt(this.bookingId));
      this.successMessage = 'تم قبول الإيصال وتأكيد الحجز.';
      this.changed.emit();
      this.close();
    } catch (error) {
      this.actionError = readBookingErrorMessage(
        readApiErrorCode(error),
        readApiError(error, 'تعذّر قبول الإيصال. حاول مرة أخرى.'),
      );
    } finally {
      this.isApproving = false;
    }
  }

  openDeclineForm(): void {
    this.showDeclineForm = true;
    this.actionError = '';
    this.declineForm.reset({
      reasonCode: 'UnreadableImage',
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
    this.successMessage = '';

    try {
      await firstValueFrom(
        this.adminBookingsService.declineReceipt(this.bookingId, {
          reasonCode: values.reasonCode,
          reasonNote: values.reasonNote?.trim() || null,
        }),
      );
      this.successMessage = 'تم رفض الإيصال وإعادة فتح مهلة الرفع للعميل.';
      this.changed.emit();
      this.close();
    } catch (error) {
      this.actionError = readBookingErrorMessage(
        readApiErrorCode(error),
        readApiError(error, 'تعذّر رفض الإيصال. حاول مرة أخرى.'),
      );
    } finally {
      this.isDeclining = false;
    }
  }
}
