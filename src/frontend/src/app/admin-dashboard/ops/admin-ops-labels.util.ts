import { AdminOpsAlertDto, AdminOpsRunbookDto } from '@contracts/ops';

const ALERT_KIND_LABELS: Record<string, string> = {
  PendingApprovalBacklog: 'تراكم الحجوزات التي تنتظر الموافقة',
  CancellationRequestNearAutoDecline: 'طلب إلغاء على وشك الرفض التلقائي',
  RefundDueAgeing: 'تأخر تسجيل المبلغ المسترد',
  JobHeartbeatStale: 'توقف تحديث حالة مهمة خلفية',
  JobFailure: 'فشل في مهمة خلفية',
  OutboxDeadLetter: 'تعذر إرسال رسالة بريد',
  OutboxDeadLetterBurst: 'تعطل إرسال عدة رسائل بريد',
};

const CONTEXT_KEY_LABELS: Record<string, string> = {
  bookingId: 'رقم الحجز',
  paymentId: 'رقم عملية الدفع',
  cancellationRequestId: 'رقم طلب الإلغاء',
  pendingSinceUtc: 'تنتظر الموافقة منذ',
  ageHours: 'المدة (بالساعات)',
  autoDeclineAtUtc: 'موعد الرفض التلقائي',
  minutesRemaining: 'الوقت المتبقي (بالدقائق)',
  cancelledAtUtc: 'تاريخ الإلغاء',
  messageId: 'رقم الرسالة',
  messageType: 'نوع الرسالة',
  aggregateId: 'رقم الكيان',
  lastError: 'آخر خطأ',
  deadLetterCount: 'عدد الرسائل التي تعذر إرسالها',
  windowHours: 'الفترة (بالساعات)',
  jobName: 'اسم المهمة',
  intervalSeconds: 'الفاصل الزمني (بالثواني)',
  intervalMultiplier: 'عدد مرات الفترة المتوقعة',
  stalenessMinutes: 'مدة التأخر (بالدقائق)',
  lastFailureAtUtc: 'وقت آخر فشل',
};

const BOOKINGS_LINK_KINDS = new Set([
  'PendingApprovalBacklog',
  'CancellationRequestNearAutoDecline',
  'RefundDueAgeing',
]);

type LocalizedRunbook = {
  responseSla: string;
  trigger: string;
  steps: string[];
};

const RUNBOOK_LOCALIZATION: Record<string, LocalizedRunbook> = {
  'pending-approval-backlog': {
    responseSla: 'تحذير: خلال 4 ساعات · حرج: خلال ساعة واحدة',
    trigger:
      'يظهر التنبيه إذا ظل الحجز بانتظار الموافقة لأكثر من 6 ساعات (تحذير) أو 24 ساعة (حرج).',
    steps: [
      'تأكد من وصول رسالة البريد إلى المسؤول، وراجع صندوق الصادر وسجلات خدمة البريد.',
      'افتح قائمة الحجوزات، واختر فلتر «انتظار الموافقة».',
      'راجع الحجوزات التي ظهر بسببها التنبيه أولًا، ثم وافق عليها أو ارفضها مع توضيح السبب.',
      'إذا كانت القائمة فارغة وما زال التنبيه موجودًا، قارن سجل تغييرات الحالة بالحالة الحالية للتأكد من عدم وجود بيانات قديمة.',
    ],
  },
  'cancellation-request-near-auto-decline': {
    responseSla: 'خلال 15 دقيقة',
    trigger: 'يظهر التنبيه إذا كان طلب الإلغاء المعلق سيُرفض تلقائيًا خلال 30 دقيقة.',
    steps: [
      'افتح الحجز فورًا من قائمة طلبات الإلغاء.',
      'وافق على الطلب أو ارفضه قبل موعد الرفض التلقائي.',
      'إذا تعذر التعامل مع الطلب، سيُعاد الحجز تلقائيًا إلى «مؤكد» وسيتم إرسال بريد للعميل.',
    ],
  },
  'refund-due-ageing': {
    responseSla: 'تحذير: خلال يوم عمل واحد · حرج: خلال 4 ساعات',
    trigger:
      'يظهر التنبيه إذا كان هناك حجز ملغى مع دفعة معتمدة ولم يتم رد المبلغ خلال 24 ساعة (تحذير) أو 72 ساعة (حرج).',
    steps: [
      'راجع سجلات التحويل اليدوي وتأكد من تفاصيل التحويل.',
      'سجل عملية الاسترداد في النظام بعد اكتمال تحويل المبلغ.',
      'صعّد الحالة إذا احتجت للتواصل مع العميل أو كان هناك خلاف على المبلغ.',
    ],
  },
  'job-heartbeat-missing': {
    responseSla: 'تحذير: خلال 30 دقيقة · حرج: خلال 15 دقيقة',
    trigger:
      'يظهر التنبيه إذا مر على آخر نجاح للمهمة أكثر من ضعفي الفترة المحددة (تحذير) أو أربعة أضعافها (حرج).',
    steps: [
      'تأكد من أن التطبيق يعمل بشكل سليم وأن المهام الخلفية مفعلة في الإعدادات.',
      'راجع سجل تشغيل المهمة لمعرفة آخر نجاح وآخر فشل وآخر خطأ.',
      'راجع سجلات التطبيق لمعرفة سبب الفشل، وأعد تشغيل التطبيق إذا توقفت المهمة عن العمل.',
      'بعد حل المشكلة، تأكد من تسجيل نجاح جديد للمهمة.',
    ],
  },
  'job-failure': {
    responseSla: 'خلال 30 دقيقة',
    trigger: 'يظهر التنبيه إذا كان آخر فشل للمهمة أحدث من آخر نجاح لها.',
    steps: [
      'راجع آخر خطأ في سجل تشغيل المهمة.',
      'أصلح السبب الأساسي للمشكلة، سواء كان في قاعدة البيانات أو التخزين أو خدمة البريد أو الكود.',
      'انتظر التشغيل التالي للمهمة أو أعد تشغيل التطبيق، ثم تأكد من تسجيل نجاح المهمة.',
    ],
  },
  'outbox-dead-letter': {
    responseSla: 'خلال ساعة واحدة',
    trigger: 'يظهر التنبيه عند وجود رسالة في صندوق الصادر تعذر إرسالها نهائيًا.',
    steps: [
      'راجع آخر خطأ ونوع الرسالة ورقم الكيان المرتبط بها.',
      'أصلح قالب الرسالة أو بيانات المستلم أو إعدادات خدمة البريد.',
      'أعد الرسالة إلى قائمة الإرسال يدويًا أو أنشئ رسالة جديدة في صندوق الصادر بعد حل المشكلة.',
    ],
  },
  'outbox-dead-letter-burst': {
    responseSla: 'خلال 15 دقيقة',
    trigger:
      'يظهر التنبيه عند تعذر إرسال 5 رسائل أو أكثر خلال ساعة واحدة، مما قد يشير إلى مشكلة عامة في النظام.',
    steps: [
      'اعتبر المشكلة عطلًا في خدمة البريد، مثل خدمة Brevo أو إعدادات DNS أو بيانات الدخول.',
      'أوقف عمليات النشر غير الضرورية، وتأكد من إعدادات البريد وحالة خدمة البريد.',
      'بعد عودة خدمة البريد للعمل، أعد إرسال الرسائل المتراكمة تدريجيًا وراقب تكرار المشكلة.',
    ],
  },
};

export function formatAlertMessage(alert: AdminOpsAlertDto): string {
  const { kind, context } = alert;

  switch (kind) {
    case 'PendingApprovalBacklog':
      return `الحجز ${context['bookingId']} ينتظر الموافقة منذ ${context['ageHours']} ساعة.`;
    case 'CancellationRequestNearAutoDecline':
      return `طلب الإلغاء ${context['cancellationRequestId']} للحجز ${context['bookingId']} سيُرفض تلقائيًا خلال ${context['minutesRemaining']} دقيقة.`;
    case 'RefundDueAgeing':
      return `يوجد مبلغ مستحق للاسترداد لعملية الدفع ${context['paymentId']}، ولم يتم تسجيل الاسترداد بعد مرور ${context['ageHours']} ساعة.`;
    case 'OutboxDeadLetter':
      return `تعذر إرسال رسالة البريد ${context['messageId']} (${context['messageType']}).`;
    case 'OutboxDeadLetterBurst':
      return `تعذر إرسال ${context['deadLetterCount']} رسالة بريد خلال آخر ${context['windowHours']} ساعة.`;
    case 'JobFailure':
      return `سجلت المهمة الخلفية ${context['jobName']} فشلًا في ${context['lastFailureAtUtc']}.`;
    case 'JobHeartbeatStale': {
      const stalenessMinutes = context['stalenessMinutes'];
      if (stalenessMinutes === 'unknown') {
        return `المهمة الخلفية ${context['jobName']} لم تسجل أي نجاح حتى الآن.`;
      }

      return `آخر نجاح للمهمة الخلفية ${context['jobName']} كان منذ ${stalenessMinutes} دقيقة، أي أكثر من ${context['intervalMultiplier']} مرة من الفترة المتوقعة.`;
    }
    default:
      return alert.message;
  }
}

export function localizeRunbook(runbook: AdminOpsRunbookDto): LocalizedRunbook {
  const localized = RUNBOOK_LOCALIZATION[runbook.id];
  if (!localized) {
    return {
      responseSla: runbook.responseSla,
      trigger: runbook.trigger,
      steps: runbook.steps,
    };
  }

  return localized;
}

export function formatContextValue(key: string, value: string): string {
  if (key === 'stalenessMinutes' && value === 'unknown') {
    return 'غير معروف';
  }

  return value;
}

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

export function formatContextEntries(
  context: Record<string, string>,
): Array<{ key: string; label: string; value: string }> {
  return Object.entries(context).map(([key, value]) => ({
    key,
    label: formatContextKey(key),
    value: formatContextValue(key, value),
  }));
}

export function getAlertActionRoute(alert: AdminOpsAlertDto): string | null {
  return BOOKINGS_LINK_KINDS.has(alert.kind) ? '/admin/bookings' : null;
}

export function compareAlertsBySeverity(a: AdminOpsAlertDto, b: AdminOpsAlertDto): number {
  const severityRank = (severity: string) =>
    severity === 'Critical' ? 0 : severity === 'Warning' ? 1 : 2;
  const severityDiff = severityRank(a.severity) - severityRank(b.severity);
  if (severityDiff !== 0) {
    return severityDiff;
  }

  return formatAlertMessage(a).localeCompare(formatAlertMessage(b));
}

export function countAlertsBySeverity(alerts: AdminOpsAlertDto[]): {
  critical: number;
  warning: number;
} {
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
