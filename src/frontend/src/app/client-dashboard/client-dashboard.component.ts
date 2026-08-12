import { Component, inject, OnInit } from '@angular/core';
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
import { formatPastBookingNotes } from './client-dashboard-labels.util';
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

  upcomingSection: DashboardSectionState = { status: 'loading' };
  pendingSection: DashboardSectionState = { status: 'loading' };
  pastSection: DashboardSectionState = { status: 'loading' };
  pastPage = MY_BOOKINGS_QUERY_LIMITS.defaultPage;
  pastLoadingMore = false;

  readonly formatPastBookingNotes = formatPastBookingNotes;
  readonly formatSlotRange = formatSlotRange;

  ngOnInit(): void {
    void this.loadAllSections();
  }

  get showFirstBookingPrompt(): boolean {
    return (
      this.upcomingSection.status === 'ready' &&
      this.pendingSection.status === 'ready' &&
      this.pastSection.status === 'ready' &&
      this.upcomingSection.totalCount === 0 &&
      this.pendingSection.totalCount === 0 &&
      this.pastSection.totalCount === 0
    );
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
    if (
      this.pastLoadingMore ||
      this.pastSection.status !== 'ready' ||
      this.pastSection.items.length >= this.pastSection.totalCount
    ) {
      return;
    }

    this.pastPage += 1;
    await this.loadPastSection(false);
  }

  get hasMorePast(): boolean {
    return (
      this.pastSection.status === 'ready' &&
      this.pastSection.items.length < this.pastSection.totalCount
    );
  }

  private async loadAllSections(): Promise<void> {
    this.upcomingSection = { status: 'loading' };
    this.pendingSection = { status: 'loading' };
    this.pastSection = { status: 'loading' };
    this.pastPage = MY_BOOKINGS_QUERY_LIMITS.defaultPage;
    this.pastLoadingMore = false;

    await Promise.all([
      this.loadSection('Upcoming'),
      this.loadSection('Pending'),
      this.loadPastSection(true),
    ]);
  }

  private async loadPastSection(reset: boolean): Promise<void> {
    if (reset) {
      this.pastPage = MY_BOOKINGS_QUERY_LIMITS.defaultPage;
      this.pastSection = { status: 'loading' };
    } else {
      this.pastLoadingMore = true;
    }

    try {
      const response = await firstValueFrom(
        this.bookingService.getMyBookings({
          status: 'Past',
          page: this.pastPage,
          pageSize: MY_BOOKINGS_QUERY_LIMITS.defaultPageSize,
        }),
      );

      const existingItems =
        !reset && this.pastSection.status === 'ready' ? this.pastSection.items : [];

      this.pastSection = {
        status: 'ready',
        items: [...existingItems, ...response.items],
        totalCount: response.totalCount,
      };
    } catch (error) {
      if (reset) {
        this.pastSection = {
          status: 'error',
          message: readApiError(error, 'تعذر تحميل السجل. حاول مرة أخرى.'),
        };
      }
    } finally {
      this.pastLoadingMore = false;
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
        message: readApiError(error, 'تعذّر تحميل الحجوزات. حاول مرة أخرى.'),
      });
    }
  }

  private assignSection(filter: MyBookingsStatusFilter, state: DashboardSectionState): void {
    switch (filter) {
      case 'Upcoming':
        this.upcomingSection = state;
        break;
      case 'Pending':
        this.pendingSection = state;
        break;
      case 'Past':
        this.pastSection = state;
        break;
    }
  }
}
