import { Component, inject } from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import { DeliveryMethod } from '@contracts/booking';
import { APP_COPY } from '../../core/i18n/app-copy.constants';
import { BookingFlowStateService } from '../booking-flow-state.service';
import { formatSlotSummary } from '../utils/slot-grouping.util';
import { BookingStepIndicatorComponent } from '../shared/booking-step-indicator.component';

@Component({
  selector: 'app-delivery-method',
  imports: [RouterLink, BookingStepIndicatorComponent],
  templateUrl: './delivery-method.component.html',
  styleUrl: './delivery-method.component.scss',
})
export class DeliveryMethodComponent {
  private readonly bookingFlow = inject(BookingFlowStateService);
  private readonly router = inject(Router);

  protected readonly copy = APP_COPY;
  readonly slotSummary = formatSlotSummary(this.bookingFlow.getState()!.slotStartUtc);

  selectDeliveryMethod(method: DeliveryMethod): void {
    this.bookingFlow.setDeliveryMethod(method);

    if (method === 'Chat') {
      void this.router.navigate(['/booking/review']);
      return;
    }

    void this.router.navigate(['/booking/phone']);
  }
}
