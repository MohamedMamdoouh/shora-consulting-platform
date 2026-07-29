export interface PaymentInstructionsResponse {
  amount: number;
  currency: string;
  vodafoneCashNumber: string;
  instaPayHandle: string;
  paymentInstructions?: string | null;
  receiptUploadDeadlineUtc: string;
}
