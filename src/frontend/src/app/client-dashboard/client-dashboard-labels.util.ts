const CANCELLATION_REASON_LABELS: Record<string, string> = {
  'Cancelled by you': 'ألغيت الحجز بنفسك',
  'Cancelled by the consultant': 'ألغى المستشار الحجز',
  'Receipt not uploaded in time': 'لم يرفع الإيصال في الوقت المحدد',
};

const REFUND_LABELS: Record<string, string> = {
  Refunded: 'تم استرداد المبلغ',
  'Refund being processed': 'الاسترداد قيد المعالجة',
};

export function localizeCancellationReasonLabel(label: string | null | undefined): string | null {
  if (!label) {
    return null;
  }

  return CANCELLATION_REASON_LABELS[label] ?? label;
}

export function localizeRefundLabel(label: string | null | undefined): string | null {
  if (!label) {
    return null;
  }

  return REFUND_LABELS[label] ?? label;
}

export function formatPastBookingNotes(
  status: string,
  cancellationReasonLabel?: string | null,
  refundLabel?: string | null,
): string | null {
  if (status !== 'Cancelled') {
    return null;
  }

  const parts = [
    localizeCancellationReasonLabel(cancellationReasonLabel),
    localizeRefundLabel(refundLabel),
  ].filter((part): part is string => Boolean(part));

  return parts.length > 0 ? parts.join(' · ') : null;
}
