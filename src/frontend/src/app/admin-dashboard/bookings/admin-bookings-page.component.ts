import { Component, computed, inject, OnInit, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule } from '@angular/forms';
import {
  AdminBookingListItem,
  AdminBookingStatusFilter,
  ADMIN_BOOKINGS_QUERY_LIMITS,
  BookingStatus,
} from '@contracts/booking';
import { firstValueFrom } from 'rxjs';
import { readApiError, readApiErrorCode } from '../../core/api/api-error.util';
import { AdminBookingsService } from '../../core/admin/admin-bookings.service';
import { readBookingErrorMessage } from '../../booking/booking-error.util';
import {
  BOOKING_STATUS_OPTIONS,
  formatAdminBookingSlot,
  formatBookingStatus,
  formatCancellationNote,
  formatDeliveryMethod,
  localDateEndExclusiveToUtcIso,
  localDateStartToUtcIso,
  totalPages,
} from './admin-bookings-labels.util';
import {
  buildDirectCancelConfirmMessage,
  canDirectCancelBooking,
  formatCancellationQueueNote,
  isCancellationRequestPending,
} from './admin-cancellation-labels.util';
import { AdminCancellationReviewPanelComponent } from './admin-cancellation-review-panel.component';
import { AdminReceiptReviewPanelComponent } from './admin-receipt-review-panel.component';
import {
  AdminRefundPanelComponent,
  AdminRefundPanelMode,
} from './admin-refund-panel.component';
import {
  canRecordRefund,
  canRevokeRefund,
  hasBookingRowActions,
  isRefundDueRow,
} from './admin-refund-labels.util';

type PageState =
  | { status: 'loading' }
  | { status: 'error'; message: string }
  | {
      status: 'ready';
      items: AdminBookingListItem[];
      page: number;
      pageSize: number;
      totalCount: number;
    };

@Component({
  selector: 'app-admin-bookings-page',
  imports: [
    ReactiveFormsModule,
    AdminReceiptReviewPanelComponent,
    AdminCancellationReviewPanelComponent,
    AdminRefundPanelComponent,
  ],
  templateUrl: './admin-bookings-page.component.html',
  styleUrl: './admin-bookings-page.component.scss',
})
export class AdminBookingsPageComponent implements OnInit {
  private readonly fb = inject(FormBuilder);
  private readonly adminBookingsService = inject(AdminBookingsService);

  readonly pageState = signal<PageState>({ status: 'loading' });
  readonly currentPage = signal<number>(ADMIN_BOOKINGS_QUERY_LIMITS.defaultPage);
  readonly receiptReviewItem = signal<AdminBookingListItem | null>(null);
  readonly cancellationReviewItem = signal<AdminBookingListItem | null>(null);
  readonly refundPanelItem = signal<AdminBookingListItem | null>(null);
  readonly refundPanelMode = signal<AdminRefundPanelMode | null>(null);
  readonly actionMessage = signal('');
  readonly actionError = signal('');
  readonly cancellingBookingId = signal<string | null>(null);

  readonly canGoPrevious = computed(() => this.currentPage() > 1);

  readonly canGoNext = computed(() => {
    const state = this.pageState();
    if (state.status !== 'ready') {
      return false;
    }

    return this.currentPage() < totalPages(state.totalCount, state.pageSize);
  });

  readonly statusOptions = BOOKING_STATUS_OPTIONS;
  readonly pageSize = ADMIN_BOOKINGS_QUERY_LIMITS.defaultPageSize;
  readonly formatBookingStatus = formatBookingStatus;
  readonly formatDeliveryMethod = formatDeliveryMethod;
  readonly formatAdminBookingSlot = formatAdminBookingSlot;
  readonly formatCancellationNote = formatCancellationNote;
  readonly formatCancellationQueueNote = formatCancellationQueueNote;
  readonly canDirectCancelBooking = canDirectCancelBooking;
  readonly isCancellationRequestPending = isCancellationRequestPending;
  readonly canRecordRefund = canRecordRefund;
  readonly canRevokeRefund = canRevokeRefund;
  readonly hasBookingRowActions = hasBookingRowActions;
  readonly isRefundDueRow = isRefundDueRow;
  readonly totalPages = totalPages;

  readonly filtersForm = this.fb.nonNullable.group({
    status: this.fb.nonNullable.control<'' | BookingStatus>(''),
    fromDate: this.fb.control<string | null>(null),
    toDate: this.fb.control<string | null>(null),
  });

  ngOnInit(): void {
    void this.loadBookings(this.currentPage());
  }

  async applyFilters(): Promise<void> {
    this.currentPage.set(ADMIN_BOOKINGS_QUERY_LIMITS.defaultPage);
    await this.loadBookings(this.currentPage());
  }

  async goToPreviousPage(): Promise<void> {
    if (!this.canGoPrevious()) {
      return;
    }

    await this.loadBookings(this.currentPage() - 1);
  }

  async goToNextPage(): Promise<void> {
    if (!this.canGoNext()) {
      return;
    }

    await this.loadBookings(this.currentPage() + 1);
  }

  openReceiptReview(item: AdminBookingListItem): void {
    this.receiptReviewItem.set(item);
    this.actionMessage.set('');
    this.actionError.set('');
  }

  closeReceiptReview(): void {
    this.receiptReviewItem.set(null);
  }

  openCancellationReview(item: AdminBookingListItem): void {
    this.cancellationReviewItem.set(item);
    this.actionMessage.set('');
    this.actionError.set('');
  }

  closeCancellationReview(): void {
    this.cancellationReviewItem.set(null);
  }

  openRecordRefund(item: AdminBookingListItem): void {
    this.refundPanelItem.set(item);
    this.refundPanelMode.set('record');
    this.actionMessage.set('');
    this.actionError.set('');
  }

  openRevokeRefund(item: AdminBookingListItem): void {
    this.refundPanelItem.set(item);
    this.refundPanelMode.set('revoke');
    this.actionMessage.set('');
    this.actionError.set('');
  }

  closeRefundPanel(): void {
    this.refundPanelItem.set(null);
    this.refundPanelMode.set(null);
  }

  async onReceiptReviewChanged(): Promise<void> {
    this.actionMessage.set('تم تحديث حالة الحجز.');
    this.receiptReviewItem.set(null);
    await this.loadBookings(this.currentPage());
  }

  async onCancellationReviewChanged(): Promise<void> {
    this.actionMessage.set('تم تحديث حالة طلب الإلغاء.');
    this.cancellationReviewItem.set(null);
    await this.loadBookings(this.currentPage());
  }

  async onRefundChanged(): Promise<void> {
    this.actionMessage.set(
      this.refundPanelMode() === 'revoke'
        ? 'تم التراجع عن تسجيل الاسترداد.'
        : 'تم تسجيل الاسترداد وإرسال تأكيد للعميل.',
    );
    this.closeRefundPanel();
    await this.loadBookings(this.currentPage());
  }

  async directCancelBooking(item: AdminBookingListItem): Promise<void> {
    if (this.cancellingBookingId()) {
      return;
    }

    const confirmed = window.confirm(buildDirectCancelConfirmMessage(item));

    if (!confirmed) {
      return;
    }

    this.cancellingBookingId.set(item.bookingId);
    this.actionError.set('');
    this.actionMessage.set('');

    try {
      await firstValueFrom(this.adminBookingsService.cancelBooking(item.bookingId));
      this.actionMessage.set(
        item.paymentStatus === 'Approved'
          ? 'تم إلغاء الحجز — استرداد مستحق.'
          : 'تم إلغاء الحجز.',
      );
      await this.loadBookings(this.currentPage());
    } catch (error) {
      this.actionError.set(
        readBookingErrorMessage(
          readApiErrorCode(error),
          readApiError(error, 'تعذر إلغاء الحجز. حاول مرة أخرى.'),
        ),
      );
    } finally {
      this.cancellingBookingId.set(null);
    }
  }

  formatRowNote(item: AdminBookingListItem): string | null {
    const queueNote = this.formatCancellationQueueNote(item);
    if (queueNote) {
      return queueNote;
    }

    return this.formatCancellationNote(item.status, item.cancellationReasonLabel, item.refundDue);
  }

  async loadBookings(page: number): Promise<void> {
    this.pageState.set({ status: 'loading' });

    try {
      const filters = this.filtersForm.getRawValue();
      const response = await firstValueFrom(
        this.adminBookingsService.listBookings({
          status: filters.status ? (filters.status as AdminBookingStatusFilter) : undefined,
          from: filters.fromDate ? localDateStartToUtcIso(filters.fromDate) : undefined,
          to: filters.toDate ? localDateEndExclusiveToUtcIso(filters.toDate) : undefined,
          page,
          pageSize: this.pageSize,
        }),
      );

      this.currentPage.set(response.page);
      this.pageState.set({
        status: 'ready',
        items: response.items,
        page: response.page,
        pageSize: response.pageSize,
        totalCount: response.totalCount,
      });
    } catch (error) {
      this.pageState.set({
        status: 'error',
        message: readApiError(error, 'تعذر تحميل الحجوزات. حاول مرة أخرى.'),
      });
    }
  }
}
