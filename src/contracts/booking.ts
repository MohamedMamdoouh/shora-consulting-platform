export type DeliveryMethod = 'VoiceCall' | 'Chat';

export type CancellationRequestStatus = 'Pending' | 'Approved' | 'Declined' | 'AutoDeclined';

export interface CreateBookingRequest {
  availabilitySlotId: string;
  deliveryMethod: DeliveryMethod;
  contactPhone?: string | null;
}

export interface PaymentInstructionsSnapshot {
  amount: number;
  currency: string;
  vodafoneCashNumber: string;
  instaPayHandle: string;
  paymentInstructions?: string | null;
  receiptUploadDeadlineUtc: string;
}

export interface ReserveBookingResponse {
  bookingId: string;
  paymentInstructions: PaymentInstructionsSnapshot;
}

export interface CancellationRequestBody {
  reason?: string | null;
}

export interface CancellationRequestResponse {
  requestId: string;
  status: CancellationRequestStatus;
  autoDeclineAtUtc: string;
  bookingStatus: string;
}
