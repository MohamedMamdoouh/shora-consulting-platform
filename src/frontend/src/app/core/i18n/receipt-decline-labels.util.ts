import { ReceiptDeclineReasonCode } from '@contracts/payments';

export const RECEIPT_DECLINE_REASON_OPTIONS: ReadonlyArray<{
  value: ReceiptDeclineReasonCode;
  label: string;
}> = [
  { value: 'UnreadableImage', label: 'صورة غير واضحة' },
  { value: 'AmountMismatch', label: 'المبلغ غير مطابق' },
  { value: 'DuplicateReceipt', label: 'إيصال مكرر' },
  { value: 'UnverifiableTransfer', label: 'تعذر التحقق من التحويل' },
  { value: 'Other', label: 'سبب آخر' },
];

export function formatReceiptDeclineReasonCode(code?: string | null): string | null {
  if (!code) {
    return null;
  }

  return RECEIPT_DECLINE_REASON_OPTIONS.find((option) => option.value === code)?.label ?? code;
}
