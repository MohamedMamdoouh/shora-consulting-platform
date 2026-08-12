import { Component, inject, OnInit } from '@angular/core';
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

  pageState: PageState = { status: 'loading' };
  currentPage: number = ADMIN_BOOKINGS_QUERY_LIMITS.defaultPage;
  receiptReviewItem: AdminBookingListItem | null = null;
  cancellationReviewItem: AdminBookingListItem | null = null;
  refundPanelItem: AdminBookingListItem | null = null;
  refundPanelMode: AdminRefundPanelMode | null = null;
  actionMessage = '';
  actionError = '';
  cancellingBookingId: string | null = null;

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
    void this.loadBookings(this.currentPage);
  }

  get canGoPrevious(): boolean {
    return this.currentPage > 1;
  }

  get canGoNext(): boolean {
    if (this.pageState.status !== 'ready') {
      return false;
    }

    return this.currentPage < totalPages(this.pageState.totalCount, this.pageState.pageSize);
  }

  async applyFilters(): Promise<void> {
    this.currentPage = ADMIN_BOOKINGS_QUERY_LIMITS.defaultPage;
    await this.loadBookings(this.currentPage);
  }

  async goToPreviousPage(): Promise<void> {
    if (!this.canGoPrevious) {
      return;
    }

    await this.loadBookings(this.currentPage - 1);
  }

  async goToNextPage(): Promise<void> {
    if (!this.canGoNext) {
      return;
    }

    await this.loadBookings(this.currentPage + 1);
  }

  openReceiptReview(item: AdminBookingListItem): void {
    this.receiptReviewItem = item;
    this.actionMessage = '';
    this.actionError = '';
  }

  closeReceiptReview(): void {
    this.receiptReviewItem = null;
  }

  openCancellationReview(item: AdminBookingListItem): void {
    this.cancellationReviewItem = item;
    this.actionMessage = '';
    this.actionError = '';
  }

  closeCancellationReview(): void {
    this.cancellationReviewItem = null;
  }

  openRecordRefund(item: AdminBookingListItem): void {
    this.refundPanelItem = item;
    this.refundPanelMode = 'record';
    this.actionMessage = '';
    this.actionError = '';
  }

  openRevokeRefund(item: AdminBookingListItem): void {
    this.refundPanelItem = item;
    this.refundPanelMode = 'revoke';
    this.actionMessage = '';
    this.actionError = '';
  }

  closeRefundPanel(): void {
    this.refundPanelItem = null;
    this.refundPanelMode = null;
  }

  async onReceiptReviewChanged(): Promise<void> {
    this.actionMessage = 'تم تحديث حالة الحجز.';
    this.receiptReviewItem = null;
    await this.loadBookings(this.currentPage);
  }

  async onCancellationReviewChanged(): Promise<void> {
    this.actionMessage = 'تم تحديث حالة طلب الإلغاء.';
    this.cancellationReviewItem = null;
    await this.loadBookings(this.currentPage);
  }

  async onRefundChanged(): Promise<void> {
    this.actionMessage =
      this.refundPanelMode === 'revoke'
        ? 'تم التراجع عن تسجيل الاسترداد.'
        : 'تم تسجيل الاسترداد وإرسال تأكيد للعميل.';
    this.closeRefundPanel();
    await this.loadBookings(this.currentPage);
  }

  async directCancelBooking(item: AdminBookingListItem): Promise<void> {
    if (this.cancellingBookingId) {
      return;
    }

    const confirmed = window.confirm(buildDirectCancelConfirmMessage(item));

    if (!confirmed) {
      return;
    }

    this.cancellingBookingId = item.bookingId;
    this.actionError = '';
    this.actionMessage = '';

    try {
      await firstValueFrom(this.adminBookingsService.cancelBooking(item.bookingId));
      this.actionMessage =
        item.paymentStatus === 'Approved'
          ? 'تم إلغاء الحجز — استرداد مستحق.'
          : 'تم إلغاء الحجز.';
      await this.loadBookings(this.currentPage);
    } catch (error) {
      this.actionError = readBookingErrorMessage(
        readApiErrorCode(error),
        readApiError(error, 'تعذر إلغاء الحجز. حاول مرة أخرى.'),
      );
    } finally {
      this.cancellingBookingId = null;
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
    this.pageState = { status: 'loading' };

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

      this.currentPage = response.page;
      this.pageState = {
        status: 'ready',
        items: response.items,
        page: response.page,
        pageSize: response.pageSize,
        totalCount: response.totalCount,
      };
    } catch (error) {
      this.pageState = {
        status: 'error',
        message: readApiError(error, 'تعذر تحميل الحجوزات. حاول مرة أخرى.'),
      };
    }
  }
}
