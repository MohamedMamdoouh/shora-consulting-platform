import { ErrorCodes } from '@contracts/error-codes';

const BOOKING_ERROR_MESSAGES: Record<string, string> = {
  [ErrorCodes.Booking.SlotUnavailable]: 'هذا الموعد لم يعد متاحًا. يرجى اختيار موعد آخر.',
  [ErrorCodes.Booking.HoldCapExceeded]:
    'لديك الحد الأقصى من الحجوزات غير المؤكدة. قم بإلغاءأحدها من لوحتك ثم حاول مجددًا.',
  [ErrorCodes.Booking.EmailNotVerified]: 'يرجى تأكيد بريدك الإلكتروني قبل حجز جلسة.',
  [ErrorCodes.Booking.ContactPhoneRequired]: 'رقم الهاتف مطلوب للمكالمة الصوتية.',
  [ErrorCodes.Booking.ContactPhoneInvalid]: 'رقم الهاتف غير صالح. استخدم رقم موبايل مصري صحيح.',
  [ErrorCodes.Booking.NotFound]: 'لم يتم العثور على الحجز.',
  [ErrorCodes.Booking.Forbidden]: 'لا يمكنك عرض تعليمات هذا الحجز.',
  [ErrorCodes.Booking.InvalidStatus]: 'تعليمات الدفع غير متاحة لهذا الحجز.',
  [ErrorCodes.Cancellation.TooLate]: 'فات الأوان لطلب الإلغاء عبر الموقع — تواصل معي على واتساب.',
  [ErrorCodes.Cancellation.ReopenExhausted]:
    'لا يمكن تقديم طلب إلغاء آخر عبر الموقع — تواصل معي على واتساب.',
  [ErrorCodes.Payment.UploadDeadlinePassed]: 'انتهت مهلة رفع الإيصال.',
  [ErrorCodes.Payment.InvalidReceiptFile]:
    'ملف الإيصال غير صالح. استخدم JPG أو PNG أو WebP أو PDF.',
  [ErrorCodes.Payment.ReceiptTooLarge]: 'حجم ملف الإيصال أكبر من 5 ميجابايت.',
  [ErrorCodes.Payment.InvalidMethod]: 'يرجى اختيار طريقة الدفع المستخدمة.',
  [ErrorCodes.Payment.InvalidStatus]: 'لا يمكن رفع إيصال لهذا الحجز في حالته الحالية.',
  [ErrorCodes.Payment.NoPendingReceipt]: 'لا يوجد إيصال قيد المراجعة لهذا الحجز.',
  [ErrorCodes.Payment.InvalidDeclineReason]: 'سبب الرفض غير صالح.',
  [ErrorCodes.Cancellation.InvalidDecisionReason]: 'سبب رفض طلب الإلغاء غير صالح.',
  [ErrorCodes.Payment.NotFound]: 'لم يتم العثور على الدفع.',
  [ErrorCodes.Payment.RefundNotDue]: 'لا يوجد استرداد مستحق لهذا الحجز.',
  [ErrorCodes.Payment.NotRefunded]: 'لم يسجل استرداد يمكن التراجع عنه.',
  [ErrorCodes.Payment.InvalidRefundReference]: 'مرجع الاسترداد مطلوب.',
  [ErrorCodes.Payment.InvalidRefundRevocationReason]: 'سبب التصحيح مطلوب.',
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
