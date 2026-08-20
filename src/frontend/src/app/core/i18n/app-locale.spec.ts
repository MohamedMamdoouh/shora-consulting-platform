import { describe, expect, it } from 'vitest';
import {
  formatDisplayUtcDateTime,
  formatLocalDateTimeWithDay,
  formatLocalDateWithDay,
  formatLocalTime,
} from './app-locale';

describe('app locale date formatting', () => {
  it('formats utc instants for display in cairo', () => {
    expect(formatDisplayUtcDateTime('2026-08-13T12:49:00.000Z')).toBe(
      'الخميس، 13 أغسطس، 2026 · 3:49 م',
    );
  });

  it('formats date-only labels with arabic commas', () => {
    expect(formatLocalDateWithDay('2026-08-13T12:49:00.000Z', { timeZone: 'Africa/Cairo' })).toBe(
      'الخميس، 13 أغسطس، 2026',
    );
  });

  it('formats local time labels', () => {
    expect(formatLocalTime('2026-08-13T12:49:00.000Z', { timeZone: 'Africa/Cairo' })).toBe('3:49 م');
  });

  it('returns invalid string values unchanged', () => {
    expect(formatLocalDateTimeWithDay('not-a-date')).toBe('not-a-date');
  });
});
