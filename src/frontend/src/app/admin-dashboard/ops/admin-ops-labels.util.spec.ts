import { describe, expect, it } from 'vitest';
import {
  compareAlertsBySeverity,
  countAlertsBySeverity,
  formatAlertKind,
  formatAlertSeverity,
  formatContextKey,
  formatContextValue,
  formatOpsUtcDateTime,
  getAlertActionRoute,
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
    const formatted = formatContextValue('cancelledAtUtc', '2026-08-14T13:19:16.3955350Z');
    expect(formatted).toContain('·');
    expect(formatOpsUtcDateTime('not-a-date')).toBe('not-a-date');
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
});
