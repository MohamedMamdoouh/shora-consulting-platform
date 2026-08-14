import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { MY_BOOKINGS_QUERY_LIMITS } from '@contracts/booking';
import { firstValueFrom } from 'rxjs';
import { readApiError } from '../core/api/api-error.util';
import { BookingService } from '../core/booking/booking.service';
import { DashboardSectionState } from './client-dashboard-section-state';
import {
  formatBookingStatusLabel,
  formatPastBookingCancellation,
} from './client-dashboard-labels.util';
import { formatSlotRange } from './client-dashboard-slot.util';

@Component({
  selector: 'app-history-bookings',
  templateUrl: './history-bookings.component.html',
  styleUrl: './history-bookings.component.scss',
})
export class HistoryBookingsComponent implements OnInit {
  private readonly bookingService = inject(BookingService);

  readonly section = signal<DashboardSectionState>({ status: 'loading' });
  readonly loadingMore = signal(false);
  private page = MY_BOOKINGS_QUERY_LIMITS.defaultPage;

  readonly hasMore = computed(() => {
    const state = this.section();
    return state.status === 'ready' && state.items.length < state.totalCount;
  });

  readonly formatSlotRange = formatSlotRange;
  readonly formatStatus = formatBookingStatusLabel;
  readonly formatPastBookingCancellation = formatPastBookingCancellation;

  ngOnInit(): void {
    void this.load(true);
  }

  async load(reset: boolean): Promise<void> {
    if (reset) {
      this.page = MY_BOOKINGS_QUERY_LIMITS.defaultPage;
      this.section.set({ status: 'loading' });
    } else {
      this.loadingMore.set(true);
    }

    try {
      const response = await firstValueFrom(
        this.bookingService.getMyBookings({
          status: 'Past',
          page: this.page,
          pageSize: MY_BOOKINGS_QUERY_LIMITS.defaultPageSize,
        }),
      );

      const current = this.section();
      const existingItems = !reset && current.status === 'ready' ? current.items : [];

      this.section.set({
        status: 'ready',
        items: [...existingItems, ...response.items],
        totalCount: response.totalCount,
      });
    } catch (error) {
      if (reset) {
        this.section.set({
          status: 'error',
          message: readApiError(error, 'تعذر تحميل السجل. حاول مرة أخرى.'),
        });
      }
    } finally {
      this.loadingMore.set(false);
    }
  }

  async loadMore(): Promise<void> {
    const state = this.section();

    if (this.loadingMore() || state.status !== 'ready' || state.items.length >= state.totalCount) {
      return;
    }

    this.page += 1;
    await this.load(false);
  }
}
