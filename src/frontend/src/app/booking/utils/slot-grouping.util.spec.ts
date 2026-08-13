import { describe, expect, it } from 'vitest';
import { formatSlotSummary, groupSlotsByLocalDay } from './slot-grouping.util';

describe('groupSlotsByLocalDay', () => {
  it('groups slots by local calendar day and sorts times within each day', () => {
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

  it('labels Monday groups as الإثنين', () => {
    const mondayNoon = new Date(2026, 7, 10, 12, 0, 0).toISOString();
    const groups = groupSlotsByLocalDay([
      {
        id: 'mon',
        startTimeUtc: mondayNoon,
        endTimeUtc: new Date(2026, 7, 10, 13, 0, 0).toISOString(),
      },
    ]);

    expect(groups[0]?.label).toContain('الإثنين');
    expect(groups[0]?.label).not.toContain('الاثنين');
    expect(formatSlotSummary(mondayNoon)).toContain('الإثنين');
  });
});
