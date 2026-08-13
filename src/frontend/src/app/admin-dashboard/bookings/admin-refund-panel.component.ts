import { Component, EventEmitter, Input, Output, Signal, computed, inject, signal } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { AbstractControl, FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { AdminBookingListItem } from '@contracts/booking';
import { firstValueFrom, map, startWith } from 'rxjs';
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

  private readonly referenceText = trimmedValue(this.recordForm.controls.reference);
  private readonly correctionReasonText = trimmedValue(this.revokeForm.controls.correctionReason);

  readonly hasReference = computed(() => this.referenceText().length > 0);
  readonly hasCorrectionReason = computed(() => this.correctionReasonText().length > 0);

  readonly referenceError = computed(() =>
    this.hasReference() ? null : this.copy.admin.dialog.refundReferenceRequired,
  );

  readonly correctionReasonError = computed(() => {
    if (!this.hasCorrectionReason()) {
      return this.copy.admin.dialog.revokeRefundReasonRequired;
    }

    if (this.correctionReasonText().length > 1000) {
      return this.copy.admin.dialog.revokeRefundReasonTooLong;
    }

    return null;
  });

  close(): void {
    this.closed.emit();
  }

  async submitRecord(): Promise<void> {
    if (this.isSubmitting() || !this.item.paymentId || !this.hasReference()) {
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
    if (this.isSubmitting() || !this.item.paymentId || !this.hasCorrectionReason()) {
      return;
    }

    const confirmed = await this.confirmDialog.confirm({
      title: this.copy.admin.dialog.revokeRefundTitle,
      message: this.copy.admin.customerAction(
        this.item.clientDisplayName,
        this.copy.admin.dialog.revokeRefundMessage,
      ),
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

function trimmedValue(control: AbstractControl<string>): Signal<string> {
  return toSignal(
    control.valueChanges.pipe(
      startWith(control.value),
      map((value) => value.trim()),
    ),
    { initialValue: control.value.trim() },
  );
}
