import { AdminOpsAlertDto } from '@contracts/ops';

const ALERT_KIND_LABELS: Record<string, string> = {
  PendingApprovalBacklog: 'تراكم انتظار الموافقة',
  CancellationRequestNearAutoDecline: 'طلب إلغاء قريب من الرفض التلقائي',
  RefundDueAgeing: 'تأخر تسجيل الاسترداد',
  JobHeartbeatStale: 'توقف نبض مهمة خلفية',
  JobFailure: 'فشل مهمة خلفية',
  OutboxDeadLetter: 'رسالة بريد معطلة',
  OutboxDeadLetterBurst: 'تعطل جماعي للبريد',
};

const CONTEXT_KEY_LABELS: Record<string, string> = {
  bookingId: 'معرف الحجز',
  paymentId: 'معرف الدفع',
  cancellationRequestId: 'معرف طلب الإلغاء',
  pendingSinceUtc: 'بانتظار الموافقة منذ',
  ageHours: 'العمر (ساعات)',
  autoDeclineAtUtc: 'موعد الرفض التلقائي',
  minutesRemaining: 'الدقائق المتبقية',
  cancelledAtUtc: 'تاريخ الإلغاء',
  messageId: 'معرف الرسالة',
  messageType: 'نوع الرسالة',
  aggregateId: 'معرف الكيان',
  lastError: 'آخر خطأ',
  deadLetterCount: 'عدد الرسائل المعطلة',
  windowHours: 'نافذة الساعات',
  jobName: 'اسم المهمة',
  intervalSeconds: 'فترة التكرار (ثوان)',
  intervalMultiplier: 'مضاعف الفترة',
  stalenessMinutes: 'دقائق التوقف',
  lastFailureAtUtc: 'آخر عملية فاشلة',
};

const BOOKINGS_LINK_KINDS = new Set([
  'PendingApprovalBacklog',
  'CancellationRequestNearAutoDecline',
  'RefundDueAgeing',
]);

export function formatAlertKind(kind: string): string {
  return ALERT_KIND_LABELS[kind] ?? kind;
}

export function formatAlertSeverity(severity: string): string {
  switch (severity) {
    case 'Critical':
      return 'حرج';
    case 'Warning':
      return 'تحذير';
    default:
      return severity;
  }
}

export function severityCssModifier(severity: string): 'warning' | 'critical' | 'default' {
  switch (severity) {
    case 'Critical':
      return 'critical';
    case 'Warning':
      return 'warning';
    default:
      return 'default';
  }
}

export function formatContextKey(key: string): string {
  return CONTEXT_KEY_LABELS[key] ?? key;
}

export function formatContextEntries(context: Record<string, string>): Array<{ key: string; label: string; value: string }> {
  return Object.entries(context).map(([key, value]) => ({
    key,
    label: formatContextKey(key),
    value,
  }));
}

export function getAlertActionRoute(alert: AdminOpsAlertDto): string | null {
  return BOOKINGS_LINK_KINDS.has(alert.kind) ? '/admin/bookings' : null;
}

export function compareAlertsBySeverity(a: AdminOpsAlertDto, b: AdminOpsAlertDto): number {
  const severityRank = (severity: string) => (severity === 'Critical' ? 0 : severity === 'Warning' ? 1 : 2);
  const severityDiff = severityRank(a.severity) - severityRank(b.severity);
  if (severityDiff !== 0) {
    return severityDiff;
  }

  return a.message.localeCompare(b.message);
}

export function countAlertsBySeverity(alerts: AdminOpsAlertDto[]): { critical: number; warning: number } {
  return alerts.reduce(
    (counts, alert) => {
      if (alert.severity === 'Critical') {
        counts.critical += 1;
      } else if (alert.severity === 'Warning') {
        counts.warning += 1;
      }

      return counts;
    },
    { critical: 0, warning: 0 },
  );
}
