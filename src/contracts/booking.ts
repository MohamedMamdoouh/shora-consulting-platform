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

export type MyBookingsStatusFilter = 'Upcoming' | 'Pending' | 'Past';

export type BookingStatus =
  | 'PendingPayment'
  | 'PendingApproval'
  | 'Confirmed'
  | 'CancellationRequested'
  | 'Completed'
  | 'Cancelled';

export const MY_BOOKINGS_QUERY_LIMITS = {
  defaultPage: 1,
  defaultPageSize: 20,
  maxPageSize: 100,
} as const;

export interface MyBookingsQuery {
  status?: MyBookingsStatusFilter;
  page?: number;
  pageSize?: number;
}

export interface MyBookingCancellationRequestMetadata {
  status: CancellationRequestStatus;
  reopenCount: number;
  clientDecisionSeenAtUtc?: string | null;
  declineReason?: string | null;
  autoDeclineAtUtc: string;
}

export interface MyBookingPaymentSummary {
  amount: number;
  currency: string;
  vodafoneCashNumber: string;
  instaPayHandle: string;
  paymentInstructions?: string | null;
  receiptUploadDeadlineUtc?: string | null;
  latestReceiptDeclineReason?: string | null;
}

export interface MyBookingListItem {
  bookingId: string;
  slotStartUtc: string;
  slotEndUtc: string;
  deliveryMethod: DeliveryMethod;
  contactPhone?: string | null;
  status: BookingStatus;
  cancellationReasonLabel?: string | null;
  cancellationDetail?: string | null;
  refundLabel?: string | null;
  cancellationRequest?: MyBookingCancellationRequestMetadata | null;
  paymentSummary?: MyBookingPaymentSummary | null;
  receiptThumbnailUrl?: string | null;
  consultantWhatsAppNumber?: string | null;
}

export interface MyBookingsResponse {
  items: MyBookingListItem[];
  page: number;
  pageSize: number;
  totalCount: number;
}

export type AdminBookingStatusFilter = BookingStatus;

export const ADMIN_BOOKINGS_QUERY_LIMITS = {
  defaultPage: 1,
  defaultPageSize: 20,
  maxPageSize: 100,
} as const;

export interface AdminBookingsQuery {
  status?: AdminBookingStatusFilter;
  from?: string;
  to?: string;
  page?: number;
  pageSize?: number;
}

export interface AdminBookingListItem {
  bookingId: string;
  clientDisplayName: string;
  deliveryMethod: DeliveryMethod;
  contactPhone?: string | null;
  slotStartUtc: string;
  slotEndUtc: string;
  status: BookingStatus;
  cancellationReasonLabel?: string | null;
  cancellationDetail?: string | null;
  paymentId?: string | null;
  paymentStatus?: string | null;
  refundDue: boolean;
  cancellationRequest?: AdminBookingCancellationRequestSummary | null;
}

export interface AdminBookingCancellationRequestSummary {
  status: CancellationRequestStatus;
  clientReason?: string | null;
  requestedAtUtc: string;
  autoDeclineAtUtc: string;
}

export interface AdminBookingsResponse {
  items: AdminBookingListItem[];
  page: number;
  pageSize: number;
  totalCount: number;
}

export type CancellationDecisionReasonCode =
  | 'TimingConflict'
  | 'InsufficientReason'
  | 'Policy'
  | 'Other';

export interface DeclineCancellationRequestBody {
  reasonCode: CancellationDecisionReasonCode;
  reasonNote?: string | null;
}

export interface AdminBookingCancellationResponse {
  bookingId: string;
  bookingStatus: string;
  paymentStatus?: string | null;
  refundDue: boolean;
}
