import { AdminBookingListItem } from '@contracts/booking';
import { canDirectCancelBooking, isCancellationRequestPending } from './admin-cancellation-labels.util';

export function canRecordRefund(item: AdminBookingListItem): boolean {
  return item.refundDue && !!item.paymentId;
}

export function isRefundDueRow(item: AdminBookingListItem): boolean {
  return canRecordRefund(item);
}

export function hasBookingRowActions(item: AdminBookingListItem): boolean {
  return (
    item.status === 'PendingApproval' ||
    isCancellationRequestPending(item) ||
    (canDirectCancelBooking(item) && !isCancellationRequestPending(item)) ||
    canRecordRefund(item)
  );
}
