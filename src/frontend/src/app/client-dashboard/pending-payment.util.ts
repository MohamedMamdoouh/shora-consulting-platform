import { MyBookingListItem, MyBookingPaymentSummary } from '@contracts/booking';
import { PaymentInstructionsResponse } from '@contracts/payments';

export function toPaymentInstructions(
  summary: MyBookingPaymentSummary,
): PaymentInstructionsResponse | null {
  if (!summary.receiptUploadDeadlineUtc) {
    return null;
  }

  return {
    amount: summary.amount,
    currency: summary.currency,
    vodafoneCashNumber: summary.vodafoneCashNumber,
    instaPayHandle: summary.instaPayHandle,
    paymentInstructions: summary.paymentInstructions,
    receiptUploadDeadlineUtc: summary.receiptUploadDeadlineUtc,
  };
}

export function getPendingPaymentInstructions(
  item: MyBookingListItem,
): PaymentInstructionsResponse | null {
  if (!item.paymentSummary) {
    return null;
  }

  return toPaymentInstructions(item.paymentSummary);
}
