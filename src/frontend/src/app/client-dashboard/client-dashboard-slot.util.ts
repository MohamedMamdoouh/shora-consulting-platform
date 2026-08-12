import { MyBookingListItem } from '@contracts/booking';
import { APP_LOCALE } from '../core/i18n/app-locale';

export function formatSlotRange(item: Pick<MyBookingListItem, 'slotStartUtc' | 'slotEndUtc'>): string {
  const start = new Date(item.slotStartUtc);
  const end = new Date(item.slotEndUtc);

  const dateFormatter = new Intl.DateTimeFormat(APP_LOCALE, {
    weekday: 'long',
    day: 'numeric',
    month: 'long',
  });
  const timeFormatter = new Intl.DateTimeFormat(APP_LOCALE, {
    hour: 'numeric',
    minute: '2-digit',
  });

  return `${dateFormatter.format(start)} · ${timeFormatter.format(start)} – ${timeFormatter.format(end)}`;
}

export function formatSlotStartTime(slotStartUtc: string): string {
  return new Intl.DateTimeFormat(APP_LOCALE, {
    hour: 'numeric',
    minute: '2-digit',
  }).format(new Date(slotStartUtc));
}
