const CLIENT_CANCELLATION_REASON_LABELS: Record<string, string> = {
  'Cancelled by you': 'تم الإلغاء من طرفك',
  'Cancelled by the instructor': 'تم الإلغاء من طرف المستشار',
  'Cancelled by the system': 'تم الإلغاء تلقائيًا من النظام',
};

const ADMIN_CANCELLATION_REASON_LABELS: Record<string, string> = {
  'Cancelled by you': 'تم الإلغاء من طرف العميل',
  'Cancelled by the instructor': 'تم الإلغاء من طرفك',
  'Cancelled by the system': 'تم الإلغاء تلقائيًا من النظام',
};

const CANCELLATION_DETAIL_LABELS: Record<string, string> = {
  'Receipt not uploaded in time': 'لم يتم رفع الإيصال في الوقت المحدد',
};

const REFUND_LABELS: Record<string, string> = {
  Refunded: 'تم استرداد المبلغ',
  'Refund being processed': 'الاسترداد قيد المعالجة',
};

export type CancellationLabelPerspective = 'client' | 'admin';

export interface PastBookingCancellationNotes {
  cancelledBy: string;
  detail: string | null;
  refund: string | null;
}

export function localizeCancellationReasonLabel(
  label: string | null | undefined,
  perspective: CancellationLabelPerspective = 'client',
): string | null {
  if (!label) {
    return null;
  }

  const labels =
    perspective === 'admin' ? ADMIN_CANCELLATION_REASON_LABELS : CLIENT_CANCELLATION_REASON_LABELS;

  return labels[label] ?? null;
}

export function localizeCancellationDetail(detail: string | null | undefined): string | null {
  if (!detail) {
    return null;
  }

  const trimmed = detail.trim();
  if (!trimmed) {
    return null;
  }

  return CANCELLATION_DETAIL_LABELS[trimmed] ?? trimmed;
}

export function localizeRefundLabel(label: string | null | undefined): string | null {
  if (!label) {
    return null;
  }

  return REFUND_LABELS[label] ?? null;
}

export function formatPastBookingCancellation(item: {
  status: string;
  cancellationReasonLabel?: string | null;
  cancellationDetail?: string | null;
  refundLabel?: string | null;
}): PastBookingCancellationNotes | null {
  if (item.status !== 'Cancelled') {
    return null;
  }

  const cancelledBy = localizeCancellationReasonLabel(item.cancellationReasonLabel, 'client');
  const detail = localizeCancellationDetail(item.cancellationDetail);
  const refund = localizeRefundLabel(item.refundLabel);

  if (!cancelledBy && !detail && !refund) {
    return null;
  }

  return {
    cancelledBy: cancelledBy ?? 'تم إلغاء الجلسة',
    detail,
    refund,
  };
}
