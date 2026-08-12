import { Component, EventEmitter, Input, Output, inject } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { BookingStatus, MyBookingListItem } from '@contracts/booking';
import { firstValueFrom } from 'rxjs';
import { readBookingErrorMessage } from '../booking/booking-error.util';
import { readApiError, readApiErrorCode } from '../core/api/api-error.util';
import { BookingService } from '../core/booking/booking.service';
import {
  buildConsultantWhatsAppContactUrl,
  canSubmitCancellationRequest,
  isCancellationPending,
  shouldShowDeclinedBanner,
  shouldShowWhatsAppFallback,
} from './upcoming-cancellation.util';
import {
  buildWhatsAppChatUrl,
  formatDeliveryMethodLabel,
  formatVoiceCallInstruction,
} from './upcoming-booking.util';

@Component({
  selector: 'app-upcoming-booking-card',
  imports: [FormsModule],
  templateUrl: './upcoming-booking-card.component.html',
  styleUrl: './upcoming-booking-card.component.scss',
})
export class UpcomingBookingCardComponent {
  private readonly bookingService = inject(BookingService);

  @Input({ required: true }) item!: MyBookingListItem;
  @Input({ required: true }) slotLabel!: string;
  @Input({ required: true }) statusLabel!: string;

  @Output() readonly changed = new EventEmitter<void>();

  readonly formatDeliveryMethodLabel = formatDeliveryMethodLabel;

  cancellationReason = '';
  cancellationError = '';
  cancellationActionError = '';
  requestingCancellation = false;
  acknowledgingDecision = false;

  get voiceCallInstruction(): string | null {
    return formatVoiceCallInstruction(this.item.contactPhone, this.item.slotStartUtc);
  }

  get whatsAppChatUrl(): string | null {
    if (this.item.deliveryMethod !== 'Chat' || !this.item.consultantWhatsAppNumber) {
      return null;
    }

    return buildWhatsAppChatUrl(
      this.item.consultantWhatsAppNumber,
      this.item.slotStartUtc,
      this.item.slotEndUtc,
    );
  }

  get consultantWhatsAppContactUrl(): string | null {
    return buildConsultantWhatsAppContactUrl(this.item.consultantWhatsAppNumber);
  }

  get showPendingCancellation(): boolean {
    return isCancellationPending(this.item);
  }

  get showDeclinedBanner(): boolean {
    return shouldShowDeclinedBanner(this.item);
  }

  get showRequestCancellation(): boolean {
    return canSubmitCancellationRequest(this.item);
  }

  get showWhatsAppFallback(): boolean {
    return shouldShowWhatsAppFallback(this.item);
  }

  get declinedReason(): string {
    return this.item.cancellationRequest?.declineReason ?? 'لم يُذكر سبب.';
  }

  get statusModifier(): string | null {
    switch (this.item.status as BookingStatus) {
      case 'CancellationRequested':
        return 'cancellation-requested';
      default:
        return null;
    }
  }

  async submitCancellationRequest(): Promise<void> {
    if (this.requestingCancellation || !this.showRequestCancellation) {
      return;
    }

    const confirmed = window.confirm(
      'طلب الإلغاء يحتاج موافقة المستشار. إذا وافق، سيسترد المبلغ يدويًا. هل تريد المتابعة؟',
    );

    if (!confirmed) {
      return;
    }

    this.requestingCancellation = true;
    this.cancellationActionError = '';

    try {
      const reason = this.cancellationReason.trim();
      await firstValueFrom(
        this.bookingService.requestCancellation(this.item.bookingId, {
          reason: reason || null,
        }),
      );

      this.cancellationReason = '';
      this.changed.emit();
    } catch (err) {
      const code = readApiErrorCode(err);
      this.cancellationActionError = readBookingErrorMessage(
        code,
        readApiError(err, 'تعذر إرسال طلب الإلغاء. حاول مرة أخرى.'),
      );
    } finally {
      this.requestingCancellation = false;
    }
  }

  async acknowledgeDecision(): Promise<void> {
    if (this.acknowledgingDecision || !this.showDeclinedBanner) {
      return;
    }

    this.acknowledgingDecision = true;
    this.cancellationActionError = '';

    try {
      await firstValueFrom(
        this.bookingService.markCancellationDecisionSeen(this.item.bookingId),
      );
      this.changed.emit();
    } catch (err) {
      const code = readApiErrorCode(err);
      this.cancellationActionError = readBookingErrorMessage(
        code,
        readApiError(err, 'تعذر تسجيل الإقرار. حاول مرة أخرى.'),
      );
    } finally {
      this.acknowledgingDecision = false;
    }
  }
}
