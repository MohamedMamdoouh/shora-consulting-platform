import { DAY_OF_WEEK_NAMES, DayOfWeek, DayOfWeekName } from '@contracts/availability';

import { formatLocalDayOfWeek } from './app-locale';

export function parseDayOfWeek(
  value: DayOfWeek | string | number | null | undefined,
): DayOfWeekName {
  if (typeof value === 'number' && Number.isInteger(value) && value >= 0 && value <= 6) {
    return DAY_OF_WEEK_NAMES[value] ?? 'Monday';
  }

  if (typeof value === 'string') {
    const trimmed = value.trim();
    if (/^[0-6]$/.test(trimmed)) {
      return DAY_OF_WEEK_NAMES[Number(trimmed)] ?? 'Monday';
    }

    const match = DAY_OF_WEEK_NAMES.find(
      (name) => name.toLowerCase() === trimmed.toLowerCase(),
    );
    if (match) {
      return match;
    }
  }

  return 'Monday';
}

export function dayOfWeekIndex(value: DayOfWeek | string | number | null | undefined): number {
  return DAY_OF_WEEK_NAMES.indexOf(parseDayOfWeek(value));
}

export function formatDayOfWeek(dayOfWeek: DayOfWeek | string | number): string {
  return formatLocalDayOfWeek(dayOfWeekIndex(dayOfWeek));
}
