import { Component, computed, inject, OnInit, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule } from '@angular/forms';
import {
  AdminBookingListItem,
  AdminBookingStatusFilter,
  ADMIN_BOOKINGS_QUERY_LIMITS,
  BookingStatus,
} from '@contracts/booking';
import { firstValueFrom } from 'rxjs';
import { readApiError } from '../../core/api/api-error.util';
import { AdminBookingsService } from '../../core/admin/admin-bookings.service';
import { ConfirmDialogService } from '../../core/ui/confirm-dialog.service';
import { APP_COPY } from '../../core/i18n/app-copy.constants';
import {
  BOOKING_STATUS_OPTIONS,
  bookingStatusDataAttr,
  formatAdminBookingSlot,
  formatBookingStatus,
  formatCancellationNote,
  formatDeliveryMethod,
  localDateEndExclusiveToUtcIso,
  localDateStartToUtcIso,
  totalPages,
} from './admin-bookings-labels.util';
import {
  buildDirectCancelConfirm,
  canDirectCancelBooking,
  formatCancellationQueueNote,
  isCancellationRequestPending,
} from './admin-cancellation-labels.util';
import { AdminCancellationReviewPanelComponent } from './admin-cancellation-review-panel.component';
import { AdminReceiptReviewPanelComponent } from './admin-receipt-review-panel.component';
import { AdminRefundPanelComponent } from './admin-refund-panel.component';
import { canRecordRefund, hasBookingRowActions, isRefundDueRow } from './admin-refund-labels.util';

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
  private readonly confirmDialog = inject(ConfirmDialogService);

  protected readonly copy = APP_COPY;

  readonly pageState = signal<PageState>({ status: 'loading' });
  readonly currentPage = signal<number>(ADMIN_BOOKINGS_QUERY_LIMITS.defaultPage);
  readonly receiptReviewItem = signal<AdminBookingListItem | null>(null);
  readonly cancellationReviewItem = signal<AdminBookingListItem | null>(null);
  readonly refundPanelItem = signal<AdminBookingListItem | null>(null);
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
  readonly bookingStatusDataAttr = bookingStatusDataAttr;
  readonly formatDeliveryMethod = formatDeliveryMethod;
  readonly formatAdminBookingSlot = formatAdminBookingSlot;
  readonly formatCancellationNote = formatCancellationNote;
  readonly formatCancellationQueueNote = formatCancellationQueueNote;
  readonly canDirectCancelBooking = canDirectCancelBooking;
  readonly isCancellationRequestPending = isCancellationRequestPending;
  readonly canRecordRefund = canRecordRefund;
  readonly hasBookingRowActions = hasBookingRowActions;
  readonly isRefundDueRow = isRefundDueRow;
  readonly totalPages = totalPages;

  readonly filtersForm = this.fb.nonNullable.group({
    status: this.fb.nonNullable.control<'' | AdminBookingStatusFilter>(''),
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
  }

  closeReceiptReview(): void {
    this.receiptReviewItem.set(null);
  }

  openCancellationReview(item: AdminBookingListItem): void {
    this.cancellationReviewItem.set(item);
  }

  closeCancellationReview(): void {
    this.cancellationReviewItem.set(null);
  }

  openRecordRefund(item: AdminBookingListItem): void {
    this.refundPanelItem.set(item);
  }

  closeRefundPanel(): void {
    this.refundPanelItem.set(null);
  }

  async onReceiptReviewChanged(): Promise<void> {
    this.receiptReviewItem.set(null);
    await this.loadBookings(this.currentPage());
  }

  async onCancellationReviewChanged(): Promise<void> {
    this.cancellationReviewItem.set(null);
    await this.loadBookings(this.currentPage());
  }

  async onRefundChanged(): Promise<void> {
    this.closeRefundPanel();
    await this.loadBookings(this.currentPage());
  }

  async directCancelBooking(item: AdminBookingListItem): Promise<void> {
    if (this.cancellingBookingId()) {
      return;
    }

    const confirmed = await this.confirmDialog.confirm({
      title: this.copy.admin.dialog.cancelBookingTitle,
      ...buildDirectCancelConfirm(item),
      confirmLabel: this.copy.admin.dialog.cancelBookingAction,
      variant: 'danger',
    });

    if (!confirmed) {
      return;
    }

    this.cancellingBookingId.set(item.bookingId);

    try {
      await firstValueFrom(this.adminBookingsService.cancelBooking(item.bookingId));
      await this.confirmDialog.result({
        message:
          item.paymentStatus === 'Approved' ? 'تم إلغاء الحجز — استرداد مستحق.' : 'تم إلغاء الحجز.',
        onComplete: () => this.loadBookings(this.currentPage()),
      });
    } catch (error) {
      await this.confirmDialog.result({
        message: readApiError(error, 'تعذر إلغاء الحجز. حاول مرة أخرى.'),
        variant: 'danger',
      });
    } finally {
      this.cancellingBookingId.set(null);
    }
  }

  formatRowNote(item: AdminBookingListItem): string | null {
    const queueNote = this.formatCancellationQueueNote(item);
    if (queueNote) {
      return queueNote;
    }

    return this.formatCancellationNote(
      item.status,
      item.cancellationReasonLabel,
      item.refundDue,
      item.cancellationDetail,
      item.paymentStatus,
    );
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
