import { Component, EventEmitter, Input, Output, inject, signal } from '@angular/core';
import { MyBookingListItem } from '@contracts/booking';
import { firstValueFrom } from 'rxjs';
import { readApiError, readApiErrorCode } from '../core/api/api-error.util';
import { BookingService } from '../core/booking/booking.service';
import { readBookingErrorMessage } from '../booking/booking-error.util';
import { APP_COPY } from '../core/i18n/app-copy.constants';
import { ConfirmDialogService } from '../core/ui/confirm-dialog.service';

@Component({
  selector: 'app-pending-approval-card',
  templateUrl: './pending-approval-card.component.html',
  styleUrl: './pending-approval-card.component.scss',
})
export class PendingApprovalCardComponent {
  private readonly bookingService = inject(BookingService);
  private readonly confirmDialog = inject(ConfirmDialogService);

  protected readonly copy = APP_COPY;

  @Input({ required: true }) item!: MyBookingListItem;
  @Input({ required: true }) slotLabel!: string;

  @Output() readonly changed = new EventEmitter<void>();

  readonly cancelError = signal('');
  readonly cancelling = signal(false);

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
        message: readBookingErrorMessage(
          readApiErrorCode(err),
          readApiError(err, 'تعذر إلغاء الحجز. حاول مرة أخرى.'),
        ),
        variant: 'danger',
      });
    } finally {
      this.cancelling.set(false);
    }
  }
}
