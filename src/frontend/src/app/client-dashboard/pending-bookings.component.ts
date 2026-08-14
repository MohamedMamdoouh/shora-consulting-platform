import { Component, OnInit, inject, signal } from '@angular/core';
import { MY_BOOKINGS_QUERY_LIMITS } from '@contracts/booking';
import { firstValueFrom } from 'rxjs';
import { readApiError } from '../core/api/api-error.util';
import { BookingService } from '../core/booking/booking.service';
import { DashboardSectionState } from './client-dashboard-section-state';
import { formatSlotRange } from './client-dashboard-slot.util';
import { PendingApprovalCardComponent } from './pending-approval-card.component';
import { PendingPaymentCardComponent } from './pending-payment-card.component';

@Component({
  selector: 'app-pending-bookings',
  imports: [PendingPaymentCardComponent, PendingApprovalCardComponent],
  templateUrl: './pending-bookings.component.html',
  styleUrl: './pending-bookings.component.scss',
})
export class PendingBookingsComponent implements OnInit {
  private readonly bookingService = inject(BookingService);

  readonly section = signal<DashboardSectionState>({ status: 'loading' });

  readonly formatSlotRange = formatSlotRange;

  ngOnInit(): void {
    void this.load();
  }

  async load(): Promise<void> {
    this.section.set({ status: 'loading' });

    try {
      const response = await firstValueFrom(
        this.bookingService.getMyBookings({
          status: 'Pending',
          page: MY_BOOKINGS_QUERY_LIMITS.defaultPage,
          pageSize: MY_BOOKINGS_QUERY_LIMITS.maxPageSize,
        }),
      );

      this.section.set({
        status: 'ready',
        items: response.items,
        totalCount: response.totalCount,
      });
    } catch (error) {
      this.section.set({
        status: 'error',
        message: readApiError(error, 'تعذر تحميل الحجوزات. حاول مرة أخرى.'),
      });
    }
  }
}
