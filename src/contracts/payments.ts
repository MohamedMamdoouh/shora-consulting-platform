export interface PaymentInstructionsResponse {
  amount: number;
  currency: string;
  vodafoneCashNumber: string;
  instaPayHandle: string;
  paymentInstructions?: string | null;
  receiptUploadDeadlineUtc: string;
}

export type PaymentMethod = 'VodafoneCash' | 'InstaPay';

export type ReceiptDeclineReasonCode =
  | 'UnreadableImage'
  | 'AmountMismatch'
  | 'DuplicateReceipt'
  | 'UnverifiableTransfer'
  | 'Other';

export interface DeclineReceiptRequest {
  reasonCode: ReceiptDeclineReasonCode;
  reasonNote?: string | null;
}

export interface AdminReceiptDecisionResponse {
  bookingId: string;
  bookingStatus: string;
  paymentStatus: string;
  receiptId: string;
  receiptReviewStatus: string;
  receiptUploadDeadlineUtc?: string | null;
}

export interface UploadReceiptResponse {
  receiptId: string;
  bookingId: string;
  bookingStatus: string;
  reviewWarnings: string[];
}

export interface AdminPaymentReceiptItem {
  receiptId: string;
  attemptNumber: number;
  originalFileName: string;
  contentType: string;
  sizeBytes: number;
  senderReference?: string | null;
  uploadedAtUtc: string;
  blobState: string;
  malwareScanStatus: string;
  reviewStatus: string;
  declineReasonCode?: string | null;
  declineReason?: string | null;
  reviewedAtUtc?: string | null;
  reviewWarnings: string[];
  imageReadUrl?: string | null;
  imageReadUrlExpiresAtUtc?: string | null;
}

export interface AdminBookingReceiptsResponse {
  bookingId: string;
  paymentId: string;
  paymentStatus: string;
  method?: PaymentMethod | null;
  amount: number;
  currency: string;
  receipts: AdminPaymentReceiptItem[];
}

export interface RecordRefundRequest {
  reference: string;
  note?: string | null;
}

export interface PaymentRefundResponse {
  paymentId: string;
  bookingId: string;
  paymentStatus: string;
  refundReference?: string | null;
  refundedAtUtc?: string | null;
}
