import { AvailabilitySlot } from '@contracts/availability';
import {
  APP_DISPLAY_TIME_ZONE,
  formatDisplayUtcDateTime,
  formatLocalDateWithDay,
  formatLocalTime,
} from '../../core/i18n/app-locale';

export interface SlotDayGroup {
  dateKey: string;
  label: string;
  slots: AvailabilitySlot[];
}

const DISPLAY_TIME_ZONE = { timeZone: APP_DISPLAY_TIME_ZONE };

export function groupSlotsByLocalDay(slots: AvailabilitySlot[]): SlotDayGroup[] {
  const groups = new Map<string, SlotDayGroup>();

  for (const slot of slots) {
    const start = new Date(slot.startTimeUtc);
    const dateKey = localDateKey(start);

    const existing = groups.get(dateKey);
    if (existing) {
      existing.slots.push(slot);
      continue;
    }

    groups.set(dateKey, {
      dateKey,
      label: formatLocalDateWithDay(start, DISPLAY_TIME_ZONE),
      slots: [slot],
    });
  }

  return Array.from(groups.values())
    .map((group) => ({
      ...group,
      slots: [...group.slots].sort(
        (left, right) =>
          new Date(left.startTimeUtc).getTime() - new Date(right.startTimeUtc).getTime(),
      ),
    }))
    .sort((left, right) => left.dateKey.localeCompare(right.dateKey));
}

export function formatSlotTime(slot: AvailabilitySlot): string {
  return formatLocalTime(slot.startTimeUtc, DISPLAY_TIME_ZONE);
}

export function formatSlotSummary(startTimeUtc: string): string {
  return formatDisplayUtcDateTime(startTimeUtc);
}

function localDateKey(date: Date): string {
  const year = date.getFullYear();
  const month = String(date.getMonth() + 1).padStart(2, '0');
  const day = String(date.getDate()).padStart(2, '0');
  return `${year}-${month}-${day}`;
}
