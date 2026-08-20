import { describe, expect, it } from 'vitest';
import { formatSlotSummary, groupSlotsByLocalDay } from './slot-grouping.util';

describe('groupSlotsByLocalDay', () => {
  it('groups slots by Cairo calendar day and sorts times within each day', () => {
    const groups = groupSlotsByLocalDay([
      {
        id: '2',
        startTimeUtc: '2026-08-01T14:00:00.000Z',
        endTimeUtc: '2026-08-01T15:00:00.000Z',
      },
      {
        id: '1',
        startTimeUtc: '2026-08-01T10:00:00.000Z',
        endTimeUtc: '2026-08-01T11:00:00.000Z',
      },
      {
        id: '3',
        startTimeUtc: '2026-08-02T10:00:00.000Z',
        endTimeUtc: '2026-08-02T11:00:00.000Z',
      },
    ]);

    expect(groups).toHaveLength(2);
    expect(groups[0]?.slots.map((slot) => slot.id)).toEqual(['1', '2']);
    expect(groups[1]?.slots.map((slot) => slot.id)).toEqual(['3']);
  });

  it('does not mix Cairo days that share a UTC calendar date around midnight', () => {
    // 23:00 Cairo on Thu 13 Aug 2026 (UTC+3) and 00:00 Cairo on Fri 14 Aug.
    const groups = groupSlotsByLocalDay([
      {
        id: 'after-midnight',
        startTimeUtc: '2026-08-13T21:00:00.000Z',
        endTimeUtc: '2026-08-13T22:00:00.000Z',
      },
      {
        id: 'before-midnight',
        startTimeUtc: '2026-08-13T20:00:00.000Z',
        endTimeUtc: '2026-08-13T21:00:00.000Z',
      },
    ]);

    expect(groups).toHaveLength(2);
    expect(groups[0]?.dateKey).toBe('2026-08-13');
    expect(groups[0]?.label).toContain('الخميس');
    expect(groups[0]?.slots.map((slot) => slot.id)).toEqual(['before-midnight']);
    expect(groups[1]?.dateKey).toBe('2026-08-14');
    expect(groups[1]?.label).toContain('الجمعة');
    expect(groups[1]?.slots.map((slot) => slot.id)).toEqual(['after-midnight']);
  });

  it('labels Monday groups as الإثنين', () => {
    const mondayNoonCairo = '2026-08-10T09:00:00.000Z';
    const groups = groupSlotsByLocalDay([
      {
        id: 'mon',
        startTimeUtc: mondayNoonCairo,
        endTimeUtc: '2026-08-10T10:00:00.000Z',
      },
    ]);

    expect(groups[0]?.label).toContain('الإثنين');
    expect(groups[0]?.label).not.toContain('الاثنين');
    expect(formatSlotSummary(mondayNoonCairo)).toContain('الإثنين');
  });
});
