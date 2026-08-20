import { AdminPaymentReceiptItem, PaymentMethod } from '@contracts/payments';
import { formatCurrency } from '../../core/i18n/app-locale';

export {
  formatReceiptDeclineReasonCode,
  RECEIPT_DECLINE_REASON_OPTIONS,
} from '../../core/i18n/receipt-decline-labels.util';

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
      return 'تم إلغاؤه';
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

export function formatMoney(amount: number, currency: string): string {
  return formatCurrency(amount, currency, 2);
}
