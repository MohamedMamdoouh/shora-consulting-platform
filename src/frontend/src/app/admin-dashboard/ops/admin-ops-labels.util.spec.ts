import { describe, expect, it } from 'vitest';
import { formatDisplayUtcDateTime } from '../../core/i18n/app-locale';
import {  compareAlertsBySeverity,
  countAlertsBySeverity,
  formatAlertKind,
  formatAlertSeverity,
  formatContextKey,
  formatContextValue,
  getAlertActionRoute,
  getAlertTrackKey,
  localizeRunbook,
  severityCssModifier,
} from './admin-ops-labels.util';

type AdminOpsAlert = Parameters<typeof getAlertActionRoute>[0];

function alert(overrides: Partial<AdminOpsAlert> = {}): AdminOpsAlert {
  return {
    kind: 'PendingApprovalBacklog',
    severity: 'Warning',
    message: 'Sample alert',
    runbookId: 'pending-approval-backlog',
    context: { bookingId: 'booking-1' },
    ...overrides,
  };
}

describe('admin ops labels util', () => {
  it('formats alert kinds and severities in Arabic', () => {
    expect(formatAlertKind('PendingApprovalBacklog')).toBe('تراكم الحجوزات التي تنتظر الموافقة');
    expect(formatAlertSeverity('Critical')).toBe('حرج');
    expect(formatAlertSeverity('Warning')).toBe('تحذير');
  });

  it('maps severity to css modifiers', () => {
    expect(severityCssModifier('Critical')).toBe('critical');
    expect(severityCssModifier('Warning')).toBe('warning');
    expect(severityCssModifier('Unknown')).toBe('default');
  });

  it('formats known context keys in Arabic', () => {
    expect(formatContextKey('bookingId')).toBe('رقم الحجز');
    expect(formatContextKey('customKey')).toBe('customKey');
  });

  it('localizes context values when needed', () => {
    expect(formatContextValue('stalenessMinutes', 'unknown')).toBe('غير معروف');
    expect(formatContextValue('bookingId', 'abc')).toBe('abc');
  });

  it('formats utc context values with local arabic day labels', () => {
    expect(formatContextValue('cancelledAtUtc', '2026-08-13T12:49:00.000Z')).toBe(
      'الخميس، 13 أغسطس، 2026 · 3:49 م',
    );
    expect(formatDisplayUtcDateTime('not-a-date')).toBe('not-a-date');
  });

  it('localizes runbooks by id', () => {
    expect(
      localizeRunbook({
        id: 'refund-due-ageing',
        owner: 'Admin / finance',
        responseSla: 'Warning: 1 business day · Critical: 4 h',
        trigger: 'English trigger',
        steps: ['English step'],
      }).responseSla,
    ).toBe('تحذير: خلال يوم عمل واحد · حرج: خلال 4 ساعات');
  });

  it('sorts critical alerts before warnings', () => {
    const sorted = [
      alert({ severity: 'Warning', message: 'b' }),
      alert({ severity: 'Critical', message: 'a' }),
    ].sort(compareAlertsBySeverity);

    expect(sorted[0].severity).toBe('Critical');
    expect(sorted[1].severity).toBe('Warning');
  });

  it('counts alerts by severity', () => {
    expect(
      countAlertsBySeverity([
        alert({ severity: 'Critical' }),
        alert({ severity: 'Warning' }),
        alert({ severity: 'Warning' }),
      ]),
    ).toEqual({ critical: 1, warning: 2 });
  });

  it('links booking-related alerts to the bookings page', () => {
    expect(getAlertActionRoute(alert({ kind: 'PendingApprovalBacklog' }))).toBe('/admin/bookings');
    expect(getAlertActionRoute(alert({ kind: 'JobFailure' }))).toBeNull();
  });

  it('builds unique track keys for same-kind alerts with different context', () => {
    const first = getAlertTrackKey(
      alert({
        context: { bookingId: 'booking-1', ageHours: '8.0' },
        message: 'Booking booking-1 has been PendingApproval for more than 6 hours.',
      }),
    );
    const second = getAlertTrackKey(
      alert({
        context: { bookingId: 'booking-2', ageHours: '9.5' },
        message: 'Booking booking-2 has been PendingApproval for more than 6 hours.',
      }),
    );

    expect(first).not.toBe(second);
    expect(
      getAlertTrackKey(
        alert({
          context: { bookingId: 'booking-1', ageHours: '8.0' },
          message: 'Booking booking-1 has been PendingApproval for more than 6 hours.',
        }),
      ),
    ).toBe(first);
  });
});
