import { PaymentMethod } from '@contracts/payments';

export function buildReceiptUploadFormData(
  image: File,
  method: PaymentMethod,
  senderReference?: string | null,
): FormData {
  const formData = new FormData();
  formData.append('image', image);
  formData.append('method', method);

  const normalizedSenderReference = senderReference?.trim();
  if (normalizedSenderReference) {
    formData.append('senderReference', normalizedSenderReference);
  }

  return formData;
}
