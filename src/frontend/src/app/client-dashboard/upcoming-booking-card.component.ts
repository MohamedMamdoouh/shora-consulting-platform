import { Component, EventEmitter, Input, Output, inject, signal } from '@angular/core';
import { BookingStatus, MyBookingListItem } from '@contracts/booking';
import { firstValueFrom } from 'rxjs';
import { readApiError } from '../core/api/api-error.util';
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
import { APP_COPY } from '../core/i18n/app-copy.constants';
import { ConfirmDialogService } from '../core/ui/confirm-dialog.service';

@Component({
  selector: 'app-upcoming-booking-card',
  templateUrl: './upcoming-booking-card.component.html',
  styleUrl: './upcoming-booking-card.component.scss',
})
export class UpcomingBookingCardComponent {
  private readonly bookingService = inject(BookingService);
  private readonly confirmDialog = inject(ConfirmDialogService);

  protected readonly copy = APP_COPY;

  @Input({ required: true }) item!: MyBookingListItem;
  @Input({ required: true }) slotLabel!: string;
  @Input({ required: true }) statusLabel!: string;

  @Output() readonly changed = new EventEmitter<void>();

  readonly formatDeliveryMethodLabel = formatDeliveryMethodLabel;

  cancellationError = '';
  readonly cancellationActionError = signal('');
  readonly requestingCancellation = signal(false);
  readonly acknowledgingDecision = signal(false);

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
    return this.item.cancellationRequest?.declineReason ?? 'لم يذكر سبب.';
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
    if (this.requestingCancellation() || !this.showRequestCancellation) {
      return;
    }

    const confirmed = await this.confirmDialog.confirm({
      title: this.copy.client.cancellationConfirmTitle,
      message: this.copy.client.cancellationConfirm,
      confirmLabel: this.copy.client.cancellationConfirmAction,
      variant: 'danger',
    });

    if (!confirmed) {
      return;
    }

    const reason = await this.confirmDialog.prompt({
      title: this.copy.client.cancellationReasonTitle,
      message: this.copy.client.cancellationReasonMessage,
      inputLabel: this.copy.client.cancellationReasonLabel,
      placeholder: this.copy.client.cancellationReasonPlaceholder,
      confirmLabel: this.copy.client.cancellationReasonAction,
      variant: 'danger',
    });

    if (reason === undefined) {
      return;
    }

    this.requestingCancellation.set(true);
    this.cancellationActionError.set('');

    try {
      await firstValueFrom(
        this.bookingService.requestCancellation(this.item.bookingId, {
          reason: reason || null,
        }),
      );

      await this.confirmDialog.result({
        message: 'تم إرسال طلب الإلغاء.',
        onComplete: () => this.changed.emit(),
      });
    } catch (err) {
      await this.confirmDialog.result({
        message: readApiError(err, 'تعذر إرسال طلب الإلغاء. حاول مرة أخرى.'),
        variant: 'danger',
      });
    } finally {
      this.requestingCancellation.set(false);
    }
  }

  async acknowledgeDecision(): Promise<void> {
    if (this.acknowledgingDecision() || !this.showDeclinedBanner) {
      return;
    }

    this.acknowledgingDecision.set(true);
    this.cancellationActionError.set('');

    try {
      await firstValueFrom(this.bookingService.markCancellationDecisionSeen(this.item.bookingId));
      await this.confirmDialog.result({
        message: 'تم تسجيل الإقرار.',
        onComplete: () => this.changed.emit(),
      });
    } catch (err) {
      await this.confirmDialog.result({
        message: readApiError(err, 'تعذر تسجيل الإقرار. حاول مرة أخرى.'),
        variant: 'danger',
      });
    } finally {
      this.acknowledgingDecision.set(false);
    }
  }
}
