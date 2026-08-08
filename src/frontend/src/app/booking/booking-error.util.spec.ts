import { ErrorCodes } from '@contracts/error-codes';
import { describe, expect, it } from 'vitest';
import { readBookingErrorMessage } from './booking-error.util';

describe('readBookingErrorMessage', () => {
  it.each([
    [ErrorCodes.Payment.UploadDeadlinePassed, 'انتهت مهلة رفع الإيصال.'],
    [ErrorCodes.Payment.InvalidReceiptFile, 'ملف الإيصال غير صالح. استخدم JPG أو PNG أو WebP أو PDF.'],
    [ErrorCodes.Payment.ReceiptTooLarge, 'حجم ملف الإيصال أكبر من 5 ميجابايت.'],
    [ErrorCodes.Payment.InvalidMethod, 'يرجى اختيار طريقة الدفع المستخدمة.'],
    [ErrorCodes.Payment.InvalidStatus, 'لا يمكن رفع إيصال لهذا الحجز في حالته الحالية.'],
    [ErrorCodes.Payment.NotFound, 'لم يتم العثور على الدفع.'],
  ])('maps payment error code %s to a checkout message', (code, expectedMessage) => {
    expect(readBookingErrorMessage(code, 'fallback')).toBe(expectedMessage);
  });

  it('keeps using the fallback for unknown payment errors', () => {
    expect(readBookingErrorMessage('payment.unhandled', 'fallback')).toBe('fallback');
  });
});
