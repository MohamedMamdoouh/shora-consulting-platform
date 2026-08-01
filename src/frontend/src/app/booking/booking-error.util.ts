import { ErrorCodes } from '@contracts/error-codes';

const BOOKING_ERROR_MESSAGES: Record<string, string> = {
  [ErrorCodes.Booking.SlotUnavailable]:
    'هذا الموعد لم يعد متاحاً. يرجى اختيار موعد آخر.',
  [ErrorCodes.Booking.HoldCapExceeded]:
    'لديك الحد الأقصى من الحجوزات غير المؤكدة. ألغِ أحدها من لوحتك ثم حاول مجدداً.',
  [ErrorCodes.Booking.EmailNotVerified]:
    'يرجى تأكيد بريدك الإلكتروني قبل حجز جلسة.',
  [ErrorCodes.Booking.ContactPhoneRequired]:
    'رقم الهاتف مطلوب للمكالمة الصوتية.',
  [ErrorCodes.Booking.ContactPhoneInvalid]:
    'رقم الهاتف غير صالح. استخدم رقم موبايل مصري صحيح.',
  [ErrorCodes.Booking.NotFound]: 'لم يتم العثور على الحجز.',
  [ErrorCodes.Booking.Forbidden]: 'لا يمكنك عرض تعليمات هذا الحجز.',
  [ErrorCodes.Booking.InvalidStatus]: 'تعليمات الدفع غير متاحة لهذا الحجز.',
  [ErrorCodes.Payment.UploadDeadlinePassed]: 'انتهت مهلة رفع الإيصال.',
  [ErrorCodes.Payment.InvalidReceiptFile]: 'ملف الإيصال غير صالح. استخدم JPG أو PNG أو WebP أو PDF.',
  [ErrorCodes.Payment.ReceiptTooLarge]: 'حجم ملف الإيصال أكبر من 5 ميجابايت.',
  [ErrorCodes.Payment.InvalidMethod]: 'يرجى اختيار طريقة الدفع المستخدمة.',
  [ErrorCodes.Payment.InvalidStatus]: 'لا يمكن رفع إيصال لهذا الحجز في حالته الحالية.',
};

export function readBookingErrorMessage(code: string | undefined, fallback: string): string {
  if (!code) {
    return fallback;
  }

  return BOOKING_ERROR_MESSAGES[code] ?? fallback;
}

export function isSlotUnavailableError(code: string | undefined): boolean {
  return code === ErrorCodes.Booking.SlotUnavailable;
}
