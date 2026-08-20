import { MyBookingListItem } from '@contracts/booking';
import {
  APP_DISPLAY_TIME_ZONE,
  formatLocalDateWithDay,
  formatLocalTime,
} from '../core/i18n/app-locale';

const DISPLAY_TIME_ZONE = { timeZone: APP_DISPLAY_TIME_ZONE };

export function formatSlotRange(item: Pick<MyBookingListItem, 'slotStartUtc' | 'slotEndUtc'>): string {
  const date = formatLocalDateWithDay(item.slotStartUtc, DISPLAY_TIME_ZONE);
  const startTime = formatLocalTime(item.slotStartUtc, DISPLAY_TIME_ZONE);
  const endTime = formatLocalTime(item.slotEndUtc, DISPLAY_TIME_ZONE);

  return `${date} · ${startTime} – ${endTime}`;
}

export function formatSlotStartTime(slotStartUtc: string): string {
  return formatLocalTime(slotStartUtc, DISPLAY_TIME_ZONE);
}
