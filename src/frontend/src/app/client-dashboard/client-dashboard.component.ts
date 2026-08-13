import { Component, computed, inject, OnInit, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { PendingApprovalCardComponent } from './pending-approval-card.component';
import { PendingPaymentCardComponent } from './pending-payment-card.component';
import {
  BookingStatus,
  MyBookingListItem,
  MyBookingsStatusFilter,
  MY_BOOKINGS_QUERY_LIMITS,
} from '@contracts/booking';
import { firstValueFrom } from 'rxjs';
import { readApiError } from '../core/api/api-error.util';
import { BookingService } from '../core/booking/booking.service';
import { formatPastBookingCancellation } from './client-dashboard-labels.util';
import { formatSlotRange } from './client-dashboard-slot.util';
import { UpcomingBookingCardComponent } from './upcoming-booking-card.component';

type DashboardSectionState =
  | { status: 'loading' }
  | { status: 'error'; message: string }
  | { status: 'ready'; items: MyBookingListItem[]; totalCount: number };

@Component({
  selector: 'app-client-dashboard',
  imports: [RouterLink, PendingPaymentCardComponent, PendingApprovalCardComponent, UpcomingBookingCardComponent],
  templateUrl: './client-dashboard.component.html',
  styleUrl: './client-dashboard.component.scss',
})
export class ClientDashboardComponent implements OnInit {
  private readonly bookingService = inject(BookingService);

  readonly upcomingSection = signal<DashboardSectionState>({ status: 'loading' });
  readonly pendingSection = signal<DashboardSectionState>({ status: 'loading' });
  readonly pastSection = signal<DashboardSectionState>({ status: 'loading' });
  pastPage = MY_BOOKINGS_QUERY_LIMITS.defaultPage;
  readonly pastLoadingMore = signal(false);

  readonly showFirstBookingPrompt = computed(() => {
    const upcoming = this.upcomingSection();
    const pending = this.pendingSection();
    const past = this.pastSection();

    return (
      upcoming.status === 'ready' &&
      pending.status === 'ready' &&
      past.status === 'ready' &&
      upcoming.totalCount === 0 &&
      pending.totalCount === 0 &&
      past.totalCount === 0
    );
  });

  readonly hasMorePast = computed(() => {
    const past = this.pastSection();
    return past.status === 'ready' && past.items.length < past.totalCount;
  });

  readonly formatPastBookingCancellation = formatPastBookingCancellation;
  readonly formatSlotRange = formatSlotRange;

  ngOnInit(): void {
    void this.loadAllSections();
  }

  formatStatus(status: BookingStatus): string {
    switch (status) {
      case 'PendingPayment':
        return 'في انتظار الدفع';
      case 'PendingApproval':
        return 'قيد مراجعة الدفع';
      case 'Confirmed':
        return 'مؤكدة';
      case 'CancellationRequested':
        return 'طلب إلغاء';
      case 'Completed':
        return 'مكتملة';
      case 'Cancelled':
        return 'ملغاة';
      default:
        return status;
    }
  }

  async reloadSection(filter: MyBookingsStatusFilter): Promise<void> {
    if (filter === 'Past') {
      await this.loadPastSection(true);
      return;
    }

    await this.loadSection(filter);
  }

  async loadMorePast(): Promise<void> {
    const past = this.pastSection();

    if (
      this.pastLoadingMore() ||
      past.status !== 'ready' ||
      past.items.length >= past.totalCount
    ) {
      return;
    }

    this.pastPage += 1;
    await this.loadPastSection(false);
  }

  private async loadAllSections(): Promise<void> {
    this.upcomingSection.set({ status: 'loading' });
    this.pendingSection.set({ status: 'loading' });
    this.pastSection.set({ status: 'loading' });
    this.pastPage = MY_BOOKINGS_QUERY_LIMITS.defaultPage;
    this.pastLoadingMore.set(false);

    await Promise.all([
      this.loadSection('Upcoming'),
      this.loadSection('Pending'),
      this.loadPastSection(true),
    ]);
  }

  private async loadPastSection(reset: boolean): Promise<void> {
    if (reset) {
      this.pastPage = MY_BOOKINGS_QUERY_LIMITS.defaultPage;
      this.pastSection.set({ status: 'loading' });
    } else {
      this.pastLoadingMore.set(true);
    }

    try {
      const response = await firstValueFrom(
        this.bookingService.getMyBookings({
          status: 'Past',
          page: this.pastPage,
          pageSize: MY_BOOKINGS_QUERY_LIMITS.defaultPageSize,
        }),
      );

      const currentPast = this.pastSection();
      const existingItems =
        !reset && currentPast.status === 'ready' ? currentPast.items : [];

      this.pastSection.set({
        status: 'ready',
        items: [...existingItems, ...response.items],
        totalCount: response.totalCount,
      });
    } catch (error) {
      if (reset) {
        this.pastSection.set({
          status: 'error',
          message: readApiError(error, 'تعذر تحميل السجل. حاول مرة أخرى.'),
        });
      }
    } finally {
      this.pastLoadingMore.set(false);
    }
  }

  private async loadSection(filter: MyBookingsStatusFilter): Promise<void> {
    this.assignSection(filter, { status: 'loading' });

    try {
      const response = await firstValueFrom(
        this.bookingService.getMyBookings({
          status: filter,
          page: MY_BOOKINGS_QUERY_LIMITS.defaultPage,
          pageSize: MY_BOOKINGS_QUERY_LIMITS.maxPageSize,
        }),
      );

      const nextState: DashboardSectionState = {
        status: 'ready',
        items: response.items,
        totalCount: response.totalCount,
      };

      this.assignSection(filter, nextState);
    } catch (error) {
      this.assignSection(filter, {
        status: 'error',
        message: readApiError(error, 'تعذر تحميل الحجوزات. حاول مرة أخرى.'),
      });
    }
  }

  private assignSection(filter: MyBookingsStatusFilter, state: DashboardSectionState): void {
    switch (filter) {
      case 'Upcoming':
        this.upcomingSection.set(state);
        break;
      case 'Pending':
        this.pendingSection.set(state);
        break;
      case 'Past':
        this.pastSection.set(state);
        break;
    }
  }
}
