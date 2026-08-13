import { BookingStatus, DeliveryMethod } from '@contracts/booking';
import {
  localizeCancellationDetail,
  localizeCancellationReasonLabel,
} from '../../client-dashboard/client-dashboard-labels.util';
import { formatSlotRange } from '../../client-dashboard/client-dashboard-slot.util';

export const BOOKING_STATUS_OPTIONS: ReadonlyArray<{ value: '' | BookingStatus; label: string }> = [
  { value: '', label: 'كل الحالات' },
  { value: 'PendingPayment', label: 'في انتظار الدفع' },
  { value: 'PendingApproval', label: 'قيد مراجعة الدفع' },
  { value: 'Confirmed', label: 'مؤكدة' },
  { value: 'CancellationRequested', label: 'طلب إلغاء' },
  { value: 'Completed', label: 'مكتملة' },
  { value: 'Cancelled', label: 'ملغاة' },
];

export function formatBookingStatus(status: BookingStatus): string {
  return BOOKING_STATUS_OPTIONS.find((option) => option.value === status)?.label ?? status;
}

export function formatDeliveryMethod(method: DeliveryMethod): string {
  switch (method) {
    case 'VoiceCall':
      return 'مكالمة';
    case 'Chat':
      return 'محادثة';
    default:
      return method;
  }
}

export function formatAdminBookingSlot(item: { slotStartUtc: string; slotEndUtc: string }): string {
  return formatSlotRange(item);
}

export function formatCancellationNote(
  status: BookingStatus,
  cancellationReasonLabel?: string | null,
  refundDue?: boolean,
  cancellationDetail?: string | null,
): string | null {
  if (status !== 'Cancelled' && !refundDue) {
    return null;
  }

  const parts: string[] = [];

  const localizedReason = localizeCancellationReasonLabel(cancellationReasonLabel, 'admin');
  if (localizedReason) {
    parts.push(localizedReason);
  }

  const localizedDetail = localizeCancellationDetail(cancellationDetail);
  if (localizedDetail) {
    parts.push(`السبب: ${localizedDetail}`);
  }

  if (refundDue) {
    parts.push('استرداد مستحق');
  }

  return parts.length > 0 ? parts.join(' · ') : null;
}

export function localDateStartToUtcIso(date: string): string {
  const [year, month, day] = date.split('-').map(Number);
  return new Date(year, month - 1, day).toISOString();
}

export function localDateEndExclusiveToUtcIso(date: string): string {
  const [year, month, day] = date.split('-').map(Number);
  const endExclusive = new Date(year, month - 1, day + 1);
  return endExclusive.toISOString();
}

export function totalPages(totalCount: number, pageSize: number): number {
  return Math.max(1, Math.ceil(totalCount / pageSize));
}
