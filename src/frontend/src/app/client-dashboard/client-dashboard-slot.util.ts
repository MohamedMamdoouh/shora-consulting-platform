import { MyBookingListItem } from '@contracts/booking';
import { formatDayOfWeek } from '../admin-dashboard/availability/availability-window.util';
import { formatDateTime } from '../core/i18n/app-locale';

export function formatSlotRange(item: Pick<MyBookingListItem, 'slotStartUtc' | 'slotEndUtc'>): string {
  const start = new Date(item.slotStartUtc);
  const date = `${formatDayOfWeek(start.getDay())} ${formatDateTime(start, {
    day: 'numeric',
    month: 'long',
  })}`;
  const startTime = formatDateTime(item.slotStartUtc, {
    hour: 'numeric',
    minute: '2-digit',
  });
  const endTime = formatDateTime(item.slotEndUtc, {
    hour: 'numeric',
    minute: '2-digit',
  });

  return `${date} · ${startTime} – ${endTime}`;
}

export function formatSlotStartTime(slotStartUtc: string): string {
  return formatDateTime(slotStartUtc, {
    hour: 'numeric',
    minute: '2-digit',
  });
}
