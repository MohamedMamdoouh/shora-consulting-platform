import { Component, EventEmitter, Input, Output, inject } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { AdminBookingListItem } from '@contracts/booking';
import { firstValueFrom } from 'rxjs';
import { readApiError, readApiErrorCode } from '../../core/api/api-error.util';
import { AdminPaymentsService } from '../../core/admin/admin-payments.service';
import { readBookingErrorMessage } from '../../booking/booking-error.util';

export type AdminRefundPanelMode = 'record' | 'revoke';

@Component({
  selector: 'app-admin-refund-panel',
  imports: [ReactiveFormsModule],
  templateUrl: './admin-refund-panel.component.html',
  styleUrl: './admin-refund-panel.component.scss',
})
export class AdminRefundPanelComponent {
  private readonly fb = inject(FormBuilder);
  private readonly adminPaymentsService = inject(AdminPaymentsService);

  @Input({ required: true }) item!: AdminBookingListItem;
  @Input({ required: true }) mode!: AdminRefundPanelMode;

  @Output() readonly closed = new EventEmitter<void>();
  @Output() readonly changed = new EventEmitter<void>();

  actionError = '';
  isSubmitting = false;

  readonly recordForm = this.fb.nonNullable.group({
    reference: this.fb.nonNullable.control('', [Validators.required, Validators.maxLength(500)]),
    note: this.fb.control<string | null>(null),
  });

  readonly revokeForm = this.fb.nonNullable.group({
    correctionReason: this.fb.nonNullable.control('', [Validators.required, Validators.maxLength(1000)]),
  });

  close(): void {
    this.closed.emit();
  }

  async submitRecord(): Promise<void> {
    if (this.isSubmitting || !this.item.paymentId) {
      return;
    }

    if (this.recordForm.invalid) {
      this.recordForm.markAllAsTouched();
      return;
    }

    const values = this.recordForm.getRawValue();
    this.isSubmitting = true;
    this.actionError = '';

    try {
      await firstValueFrom(
        this.adminPaymentsService.recordRefund(this.item.paymentId, {
          reference: values.reference.trim(),
          note: values.note?.trim() || null,
        }),
      );
      this.changed.emit();
      this.close();
    } catch (error) {
      this.actionError = readBookingErrorMessage(
        readApiErrorCode(error),
        readApiError(error, 'تعذّر تسجيل الاسترداد. حاول مرة أخرى.'),
      );
    } finally {
      this.isSubmitting = false;
    }
  }

  async submitRevoke(): Promise<void> {
    if (this.isSubmitting || !this.item.paymentId) {
      return;
    }

    if (this.revokeForm.invalid) {
      this.revokeForm.markAllAsTouched();
      return;
    }

    const confirmed = window.confirm(
      'هل تريد التراجع عن تسجيل الاسترداد؟ سيعود الحجز إلى حالة «استرداد مستحق».',
    );

    if (!confirmed) {
      return;
    }

    const values = this.revokeForm.getRawValue();
    this.isSubmitting = true;
    this.actionError = '';

    try {
      await firstValueFrom(
        this.adminPaymentsService.revokeRefund(this.item.paymentId, {
          correctionReason: values.correctionReason.trim(),
        }),
      );
      this.changed.emit();
      this.close();
    } catch (error) {
      this.actionError = readBookingErrorMessage(
        readApiErrorCode(error),
        readApiError(error, 'تعذّر التراجع عن تسجيل الاسترداد. حاول مرة أخرى.'),
      );
    } finally {
      this.isSubmitting = false;
    }
  }
}
