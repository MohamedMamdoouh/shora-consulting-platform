import { AvailabilitySlot } from '@contracts/availability';
import { formatDayOfWeek } from '../../admin-dashboard/availability/availability-window.util';
import { formatDateTime } from '../../core/i18n/app-locale';

export interface SlotDayGroup {
  dateKey: string;
  label: string;
  slots: AvailabilitySlot[];
}

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
      label: `${formatDayOfWeek(start.getDay())} ${formatDateTime(start, {
        day: 'numeric',
        month: 'long',
      })}`,
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
  return formatDateTime(slot.startTimeUtc, {
    hour: 'numeric',
    minute: '2-digit',
    hour12: true,
  });
}

export function formatSlotSummary(startTimeUtc: string): string {
  const start = new Date(startTimeUtc);
  const date = `${formatDayOfWeek(start.getDay())} ${formatDateTime(start, {
    day: 'numeric',
    month: 'long',
  })}`;
  const time = formatDateTime(start, {
    hour: 'numeric',
    minute: '2-digit',
    hour12: true,
  });

  return `${date} — ${time}`;
}

function localDateKey(date: Date): string {
  const year = date.getFullYear();
  const month = String(date.getMonth() + 1).padStart(2, '0');
  const day = String(date.getDate()).padStart(2, '0');
  return `${year}-${month}-${day}`;
}
