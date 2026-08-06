export interface PublicSettings {
  sessionPrice: number;
  sessionDurationMinutes: number;
}

export interface AdminSettings {
  sessionPrice: number;
  sessionDurationMinutes: number;
  bufferMinutes: number;
  receiptUploadWindowMinutes: number;
  cancellationRequestAutoDeclineHours: number;
  consultantWhatsAppNumber: string;
  vodafoneCashNumber: string;
  instaPayHandle: string;
  paymentInstructions?: string | null;
  receiptRetentionMonths: number;
}

export interface UpdateAdminSettingsRequest {
  sessionPrice: number;
  sessionDurationMinutes: number;
  bufferMinutes: number;
  receiptUploadWindowMinutes: number;
  cancellationRequestAutoDeclineHours: number;
  consultantWhatsAppNumber: string;
  vodafoneCashNumber: string;
  instaPayHandle: string;
  paymentInstructions?: string | null;
}
