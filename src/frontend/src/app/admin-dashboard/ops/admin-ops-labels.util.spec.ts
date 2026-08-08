import { describe, expect, it } from 'vitest';
import {
  compareAlertsBySeverity,
  countAlertsBySeverity,
  formatAlertKind,
  formatAlertSeverity,
  formatContextKey,
  getAlertActionRoute,
  severityCssModifier,
} from './admin-ops-labels.util';

type AdminOpsAlert = Parameters<typeof getAlertActionRoute>[0];

function alert(overrides: Partial<AdminOpsAlert> = {}): AdminOpsAlert {  return {
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
    expect(formatAlertKind('PendingApprovalBacklog')).toBe('تراكم انتظار الموافقة');
    expect(formatAlertSeverity('Critical')).toBe('حرج');
    expect(formatAlertSeverity('Warning')).toBe('تحذير');
  });

  it('maps severity to css modifiers', () => {
    expect(severityCssModifier('Critical')).toBe('critical');
    expect(severityCssModifier('Warning')).toBe('warning');
    expect(severityCssModifier('Unknown')).toBe('default');
  });

  it('formats known context keys in Arabic', () => {
    expect(formatContextKey('bookingId')).toBe('معرّف الحجز');
    expect(formatContextKey('customKey')).toBe('customKey');
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
