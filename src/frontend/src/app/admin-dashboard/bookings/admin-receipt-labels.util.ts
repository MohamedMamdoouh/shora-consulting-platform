import {
  AdminPaymentReceiptItem,
  PaymentMethod,
  ReceiptDeclineReasonCode,
} from '@contracts/payments';
import { formatCurrency, formatDateTime } from '../../core/i18n/app-locale';

export const RECEIPT_DECLINE_REASON_OPTIONS: ReadonlyArray<{
  value: ReceiptDeclineReasonCode;
  label: string;
}> = [
  { value: 'UnreadableImage', label: 'صورة غير واضحة' },
  { value: 'AmountMismatch', label: 'المبلغ غير مطابق' },
  { value: 'DuplicateReceipt', label: 'إيصال مكرر' },
  { value: 'UnverifiableTransfer', label: 'تعذّر التحقق من التحويل' },
  { value: 'Other', label: 'سبب آخر' },
];

export function formatReceiptDeclineReasonCode(code?: string | null): string | null {
  if (!code) {
    return null;
  }

  return RECEIPT_DECLINE_REASON_OPTIONS.find((option) => option.value === code)?.label ?? code;
}

export function formatPaymentMethod(method?: PaymentMethod | null): string {
  switch (method) {
    case 'VodafoneCash':
      return 'فودافون كاش';
    case 'InstaPay':
      return 'إنستا باي';
    default:
      return method ?? '—';
  }
}

export function formatPaymentStatus(status: string): string {
  switch (status) {
    case 'AwaitingReceipt':
      return 'في انتظار الإيصال';
    case 'UnderReview':
      return 'قيد المراجعة';
    case 'Approved':
      return 'مقبول';
    case 'Refunded':
      return 'مسترد';
    case 'Void':
      return 'ملغى';
    default:
      return status;
  }
}

export function formatReceiptReviewStatus(status: string): string {
  switch (status) {
    case 'Pending':
      return 'قيد المراجعة';
    case 'Approved':
      return 'مقبول';
    case 'Declined':
      return 'مرفوض';
    default:
      return status;
  }
}

export function formatMalwareScanStatus(status: string): string {
  switch (status) {
    case 'Clean':
      return 'آمن';
    case 'Blocked':
      return 'محظور';
    case 'Suspicious':
      return 'مشبوه';
    case 'Pending':
      return 'قيد الفحص';
    default:
      return status;
  }
}

export function formatReviewWarning(code: string): string {
  switch (code) {
    case 'DuplicateContentHash':
      return 'محتوى مطابق لإيصال سابق';
    default:
      return code;
  }
}

export function findPendingReceipt(
  receipts: AdminPaymentReceiptItem[],
): AdminPaymentReceiptItem | undefined {
  return receipts.find((receipt) => receipt.reviewStatus === 'Pending');
}

export function formatReceiptUploadedAt(uploadedAtUtc: string): string {
  return formatDateTime(uploadedAtUtc, {
    dateStyle: 'medium',
    timeStyle: 'short',
  });
}

export function formatMoney(amount: number, currency: string): string {
  return formatCurrency(amount, currency, 2);
}
