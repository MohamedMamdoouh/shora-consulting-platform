import { Component, OnInit, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { MY_BOOKINGS_QUERY_LIMITS } from '@contracts/booking';
import { firstValueFrom } from 'rxjs';
import { readApiError } from '../core/api/api-error.util';
import { BookingService } from '../core/booking/booking.service';
import { DashboardSectionState } from './client-dashboard-section-state';
import { formatBookingStatusLabel } from './client-dashboard-labels.util';
import { formatSlotRange } from './client-dashboard-slot.util';
import { UpcomingBookingCardComponent } from './upcoming-booking-card.component';

@Component({
  selector: 'app-upcoming-bookings',
  imports: [RouterLink, UpcomingBookingCardComponent],
  templateUrl: './upcoming-bookings.component.html',
  styleUrl: './upcoming-bookings.component.scss',
})
export class UpcomingBookingsComponent implements OnInit {
  private readonly bookingService = inject(BookingService);

  readonly section = signal<DashboardSectionState>({ status: 'loading' });

  readonly formatSlotRange = formatSlotRange;
  readonly formatStatus = formatBookingStatusLabel;

  ngOnInit(): void {
    void this.load();
  }

  async load(): Promise<void> {
    this.section.set({ status: 'loading' });

    try {
      const response = await firstValueFrom(
        this.bookingService.getMyBookings({
          status: 'Upcoming',
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
        message: readApiError(error, 'تعذر تحميل الجلسات القادمة. حاول مرة أخرى.'),
      });
    }
  }
}
