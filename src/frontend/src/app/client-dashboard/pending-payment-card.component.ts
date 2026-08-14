import { Component, EventEmitter, Input, Output, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { MyBookingListItem } from '@contracts/booking';
import { firstValueFrom } from 'rxjs';
import { PaymentInstructionsPanelComponent } from '../booking/shared/payment-instructions-panel.component';
import { readApiError } from '../core/api/api-error.util';
import { BookingService } from '../core/booking/booking.service';
import { getPendingPaymentInstructions } from './pending-payment.util';
import { APP_COPY } from '../core/i18n/app-copy.constants';
import { ConfirmDialogService } from '../core/ui/confirm-dialog.service';
import { buildReceiptUploadedResult } from './pending-payment-card-dialog.util';

@Component({
  selector: 'app-pending-payment-card',
  imports: [PaymentInstructionsPanelComponent, RouterLink],
  templateUrl: './pending-payment-card.component.html',
  styleUrl: './pending-payment-card.component.scss',
})
export class PendingPaymentCardComponent {
  private readonly bookingService = inject(BookingService);
  private readonly confirmDialog = inject(ConfirmDialogService);

  protected readonly copy = APP_COPY;

  @Input({ required: true }) item!: MyBookingListItem;
  @Input({ required: true }) slotLabel!: string;

  @Output() readonly changed = new EventEmitter<void>();

  readonly cancelError = signal('');
  readonly cancelling = signal(false);

  get instructions() {
    return getPendingPaymentInstructions(this.item);
  }

  async cancelHold(): Promise<void> {
    if (this.cancelling()) {
      return;
    }

    const confirmed = await this.confirmDialog.confirm({
      title: this.copy.client.cancelHoldTitle,
      message: this.copy.client.cancelHoldConfirm,
      confirmLabel: this.copy.client.cancelHoldAction,
      variant: 'danger',
    });

    if (!confirmed) {
      return;
    }

    this.cancelling.set(true);
    this.cancelError.set('');

    try {
      await firstValueFrom(this.bookingService.cancelHold(this.item.bookingId));
      await this.confirmDialog.result({
        message: 'تم إلغاء الحجز.',
        onComplete: () => this.changed.emit(),
      });
    } catch (err) {
      await this.confirmDialog.result({
        message: readApiError(err, 'تعذر إلغاء الحجز. حاول مرة أخرى.'),
        variant: 'danger',
      });
    } finally {
      this.cancelling.set(false);
    }
  }

  async onReceiptSubmitted(): Promise<void> {
    await this.confirmDialog.result(
      buildReceiptUploadedResult(this.copy.client.receiptUploaded, () => this.changed.emit()),
    );
  }
}
