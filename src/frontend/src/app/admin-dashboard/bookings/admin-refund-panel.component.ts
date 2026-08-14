import {
  Component,
  EventEmitter,
  Input,
  Output,
  Signal,
  computed,
  inject,
  signal,
} from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { AbstractControl, FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { AdminBookingListItem } from '@contracts/booking';
import { firstValueFrom, map, startWith } from 'rxjs';
import { readApiError, readApiErrorCode } from '../../core/api/api-error.util';
import { AdminPaymentsService } from '../../core/admin/admin-payments.service';
import { ConfirmDialogService } from '../../core/ui/confirm-dialog.service';
import { APP_COPY } from '../../core/i18n/app-copy.constants';
import { readBookingErrorMessage } from '../../booking/booking-error.util';

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

  @Output() readonly closed = new EventEmitter<void>();
  @Output() readonly changed = new EventEmitter<void>();

  readonly isSubmitting = signal(false);
  readonly referenceTouched = signal(false);

  readonly recordForm = this.fb.nonNullable.group({
    reference: this.fb.nonNullable.control('', [Validators.required, Validators.maxLength(500)]),
    note: this.fb.control<string | null>(null),
  });

  private readonly referenceText = trimmedValue(this.recordForm.controls.reference);

  readonly hasReference = computed(() => this.referenceText().length > 0);

  readonly referenceError = computed(() =>
    this.referenceTouched() && !this.hasReference()
      ? this.copy.admin.dialog.refundReferenceRequired
      : null,
  );

  close(): void {
    this.closed.emit();
  }

  markReferenceTouched(): void {
    this.referenceTouched.set(true);
    this.recordForm.controls.reference.markAsTouched();
  }

  async submitRecord(): Promise<void> {
    if (this.isSubmitting() || !this.item.paymentId) {
      return;
    }

    if (!this.hasReference()) {
      this.markReferenceTouched();
      return;
    }

    const values = this.recordForm.getRawValue();

    const confirmed = await this.confirmDialog.confirm({
      title: this.copy.admin.dialog.recordRefundTitle,
      message: this.copy.admin.dialog.recordRefundMessage,
      confirmLabel: this.copy.admin.dialog.recordRefundAction,
      variant: 'danger',
    });

    if (!confirmed) {
      return;
    }

    this.isSubmitting.set(true);

    try {
      await firstValueFrom(
        this.adminPaymentsService.recordRefund(this.item.paymentId, {
          reference: values.reference.trim(),
          note: values.note?.trim() || null,
        }),
      );
      await this.confirmDialog.result({
        message: 'تم تسجيل الاسترداد وإرسال تأكيد للعميل.',
        onComplete: () => {
          this.changed.emit();
          this.close();
        },
      });
    } catch (error) {
      await this.confirmDialog.result({
        message: readBookingErrorMessage(
          readApiErrorCode(error),
          readApiError(error, 'تعذر تسجيل الاسترداد. حاول مرة أخرى.'),
        ),
        variant: 'danger',
      });
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
