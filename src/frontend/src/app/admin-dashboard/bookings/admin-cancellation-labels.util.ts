import { AdminBookingListItem, CancellationDecisionReasonCode } from '@contracts/booking';
import { APP_COPY } from '../../core/i18n/app-copy.constants';
import { formatDateTime, formatNumber } from '../../core/i18n/app-locale';

export const CANCELLATION_DECISION_REASON_OPTIONS: ReadonlyArray<{
  value: CancellationDecisionReasonCode;
  label: string;
}> = [
  { value: 'TimingConflict', label: 'تعارض في المواعيد' },
  { value: 'InsufficientReason', label: 'السبب غير كافي' },
  { value: 'Policy', label: 'سياسة الإلغاء' },
  { value: 'Other', label: 'سبب آخر' },
];

export function isCancellationRequestPending(item: AdminBookingListItem): boolean {
  return item.status === 'CancellationRequested' && item.cancellationRequest?.status === 'Pending';
}

export function canDirectCancelBooking(item: AdminBookingListItem, nowMs = Date.now()): boolean {
  switch (item.status) {
    case 'PendingPayment':
    case 'PendingApproval':
      return true;
    case 'Confirmed':
    case 'CancellationRequested':
      return nowMs < new Date(item.slotStartUtc).getTime();
    default:
      return false;
  }
}

export function formatRequestedAt(requestedAtUtc: string): string {
  return formatDateTime(requestedAtUtc, {
    dateStyle: 'medium',
    timeStyle: 'short',
  });
}

export function formatRemainingTime(deadlineUtc: string, nowMs = Date.now()): string {
  const remainingMs = new Date(deadlineUtc).getTime() - nowMs;

  if (remainingMs <= 0) {
    return 'انتهت مهلة القرار';
  }

  const totalMinutes = Math.floor(remainingMs / 60_000);
  const hours = Math.floor(totalMinutes / 60);
  const minutes = totalMinutes % 60;

  if (hours > 0) {
    return `${formatNumber(hours)} ساعة ${formatNumber(minutes)} دقيقة`;
  }

  return `${formatNumber(minutes)} دقيقة`;
}

export function formatCancellationQueueNote(item: AdminBookingListItem): string | null {
  if (!isCancellationRequestPending(item) || !item.cancellationRequest) {
    return null;
  }

  const reason = item.cancellationRequest.clientReason?.trim();
  const parts: string[] = [];

  if (reason) {
    parts.push(reason);
  }

  parts.push(`مهلة القرار: ${formatRemainingTime(item.cancellationRequest.autoDeclineAtUtc)}`);

  return parts.join(' · ');
}

export function buildDirectCancelConfirm(item: AdminBookingListItem): { message: string } {
  const refundNote = item.paymentStatus === 'Approved' ? APP_COPY.admin.dialog.refundDueNote : '';

  return {
    message: APP_COPY.admin.dialog.cancelBookingMessage(refundNote),
  };
}
