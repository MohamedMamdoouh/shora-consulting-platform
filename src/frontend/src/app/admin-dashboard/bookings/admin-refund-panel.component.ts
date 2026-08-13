import { Component, EventEmitter, Input, Output, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { AdminBookingListItem } from '@contracts/booking';
import { firstValueFrom } from 'rxjs';
import { readApiError, readApiErrorCode } from '../../core/api/api-error.util';
import { AdminPaymentsService } from '../../core/admin/admin-payments.service';
import { ConfirmDialogService } from '../../core/ui/confirm-dialog.service';
import { APP_COPY } from '../../core/i18n/app-copy.constants';
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
  private readonly confirmDialog = inject(ConfirmDialogService);

  protected readonly copy = APP_COPY;

  @Input({ required: true }) item!: AdminBookingListItem;
  @Input({ required: true }) mode!: AdminRefundPanelMode;

  @Output() readonly closed = new EventEmitter<void>();
  @Output() readonly changed = new EventEmitter<void>();

  readonly actionError = signal('');
  readonly isSubmitting = signal(false);

  readonly recordForm = this.fb.nonNullable.group({
    reference: this.fb.nonNullable.control('', [Validators.required, Validators.maxLength(500)]),
    note: this.fb.control<string | null>(null),
  });

  readonly revokeForm = this.fb.nonNullable.group({
    correctionReason: this.fb.nonNullable.control('', [Validators.required, Validators.maxLength(1000)]),
  });

  hasCorrectionReason(): boolean {
    return this.revokeForm.controls.correctionReason.value.trim().length > 0;
  }

  correctionReasonError(): string | null {
    if (!this.hasCorrectionReason()) {
      return this.copy.admin.dialog.revokeRefundReasonRequired;
    }

    if (this.revokeForm.controls.correctionReason.hasError('maxlength')) {
      return this.copy.admin.dialog.revokeRefundReasonTooLong;
    }

    return null;
  }

  close(): void {
    this.closed.emit();
  }

  async submitRecord(): Promise<void> {
    if (this.isSubmitting() || !this.item.paymentId) {
      return;
    }

    if (this.recordForm.invalid) {
      this.recordForm.markAllAsTouched();
      return;
    }

    const values = this.recordForm.getRawValue();
    this.isSubmitting.set(true);
    this.actionError.set('');

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
      this.actionError.set(
        readBookingErrorMessage(
          readApiErrorCode(error),
          readApiError(error, 'تعذر تسجيل الاسترداد. حاول مرة أخرى.'),
        ),
      );
    } finally {
      this.isSubmitting.set(false);
    }
  }

  async submitRevoke(): Promise<void> {
    if (this.isSubmitting() || !this.item.paymentId) {
      return;
    }

    if (this.revokeForm.invalid || !this.hasCorrectionReason()) {
      this.revokeForm.markAllAsTouched();
      return;
    }

    const confirmed = await this.confirmDialog.confirm({
      title: this.copy.admin.dialog.revokeRefundTitle,
      message: this.copy.admin.dialog.revokeRefundMessage,
      detail: this.copy.admin.customerName(this.item.clientDisplayName),
      confirmLabel: this.copy.admin.dialog.revokeRefundAction,
      variant: 'danger',
    });

    if (!confirmed) {
      return;
    }

    const values = this.revokeForm.getRawValue();
    this.isSubmitting.set(true);
    this.actionError.set('');

    try {
      await firstValueFrom(
        this.adminPaymentsService.revokeRefund(this.item.paymentId, {
          correctionReason: values.correctionReason.trim(),
        }),
      );
      this.changed.emit();
      this.close();
    } catch (error) {
      this.actionError.set(
        readBookingErrorMessage(
          readApiErrorCode(error),
          readApiError(error, 'تعذر التراجع عن تسجيل الاسترداد. حاول مرة أخرى.'),
        ),
      );
    } finally {
      this.isSubmitting.set(false);
    }
  }
}
