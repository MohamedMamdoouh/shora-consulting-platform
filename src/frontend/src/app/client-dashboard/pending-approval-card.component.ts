import { Component, EventEmitter, Input, Output, inject } from '@angular/core';
import { MyBookingListItem } from '@contracts/booking';
import { firstValueFrom } from 'rxjs';
import { readApiError, readApiErrorCode } from '../core/api/api-error.util';
import { BookingService } from '../core/booking/booking.service';
import { readBookingErrorMessage } from '../booking/booking-error.util';
import { APP_COPY } from '../core/i18n/app-copy.constants';

@Component({
  selector: 'app-pending-approval-card',
  templateUrl: './pending-approval-card.component.html',
  styleUrl: './pending-approval-card.component.scss',
})
export class PendingApprovalCardComponent {
  private readonly bookingService = inject(BookingService);

  protected readonly copy = APP_COPY;

  @Input({ required: true }) item!: MyBookingListItem;
  @Input({ required: true }) slotLabel!: string;

  @Output() readonly changed = new EventEmitter<void>();

  cancelError = '';
  cancelling = false;

  async cancelHold(): Promise<void> {
    if (this.cancelling) {
      return;
    }

    const confirmed = window.confirm(
      'هل تريد إلغاء هذا الحجز؟ سيتم تحرير الموعد ويمكنك حجز موعد آخر لاحقًا.',
    );

    if (!confirmed) {
      return;
    }

    this.cancelling = true;
    this.cancelError = '';

    try {
      await firstValueFrom(this.bookingService.cancelHold(this.item.bookingId));
      this.changed.emit();
    } catch (err) {
      const code = readApiErrorCode(err);
      this.cancelError = readBookingErrorMessage(
        code,
        readApiError(err, 'تعذر إلغاء الحجز. حاول مرة أخرى.'),
      );
    } finally {
      this.cancelling = false;
    }
  }
}
