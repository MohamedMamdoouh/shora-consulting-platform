import { describe, expect, it } from 'vitest';
import {
  dayOfWeekIndex,
  formatDayOfWeek,
  formatWindowSummary,
  parseDayOfWeek,
  sortWindows,
} from './availability-window.util';

describe('availability window day labels', () => {
  it('maps API weekday names and numeric values to Arabic labels', () => {
    expect(formatDayOfWeek('Monday')).toBe('الإثنين');
    expect(formatDayOfWeek('Tuesday')).toBe('الثلاثاء');
    expect(formatDayOfWeek('wednesday')).toBe('الأربعاء');
    expect(formatDayOfWeek(4)).toBe('الخميس');
    expect(formatDayOfWeek('4')).toBe('الخميس');
  });

  it('normalizes mixed API payloads to canonical weekday names', () => {
    expect(parseDayOfWeek('Tuesday')).toBe('Tuesday');
    expect(parseDayOfWeek(2)).toBe('Tuesday');
    expect(parseDayOfWeek('2')).toBe('Tuesday');
  });

  it('shows Arabic weekdays in window summaries used by the list and delete dialog', () => {
    expect(
      formatWindowSummary({
        id: '1',
        dayOfWeek: 'Tuesday',
        startTime: '16:00:00',
        endTime: '21:00:00',
        isActive: true,
      }),
    ).toBe('الثلاثاء 16:00–21:00');
  });

  it('sorts windows by week order even when the API sends English names', () => {
    const sorted = sortWindows([
      {
        id: 'thu',
        dayOfWeek: 'Thursday',
        startTime: '16:00:00',
        endTime: '21:00:00',
        isActive: true,
      },
      {
        id: 'tue',
        dayOfWeek: 'Tuesday',
        startTime: '16:00:00',
        endTime: '21:00:00',
        isActive: true,
      },
    ]);

    expect(sorted.map((window) => window.id)).toEqual(['tue', 'thu']);
    expect(dayOfWeekIndex('Tuesday')).toBe(2);
  });
});
