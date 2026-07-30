import { Component, inject } from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import { DeliveryMethod } from '@contracts/booking';
import { BookingFlowStateService } from '../booking-flow-state.service';
import { formatSlotSummary } from '../utils/slot-grouping.util';

@Component({
  selector: 'app-delivery-method',
  imports: [RouterLink],
  templateUrl: './delivery-method.component.html',
  styleUrl: './delivery-method.component.scss',
})
export class DeliveryMethodComponent {
  private readonly bookingFlow = inject(BookingFlowStateService);
  private readonly router = inject(Router);

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
