import { ErrorCodes } from '@contracts/error-codes';

export const API_ERROR_MESSAGES: Partial<Record<string, string>> = {
  [ErrorCodes.Auth.InvalidCredentials]: 'البريد الإلكتروني أو كلمة المرور غير صحيحة.',
  [ErrorCodes.Auth.DuplicateEmail]: 'هذا البريد الإلكتروني مسجل بالفعل.',
  [ErrorCodes.Auth.RefreshTokenMissing]: 'انتهت الجلسة. يرجى تسجيل الدخول مرة أخرى.',
  [ErrorCodes.Auth.RefreshTokenInvalid]: 'انتهت الجلسة. يرجى تسجيل الدخول مرة أخرى.',
  [ErrorCodes.Auth.RefreshTokenReuse]: 'تم إنهاء جلساتك لأسباب أمنية. يرجى تسجيل الدخول مرة أخرى.',
  [ErrorCodes.Auth.VerificationFailed]: 'رابط التحقق منتهي أو غير صالح.',
  [ErrorCodes.Auth.ResetFailed]: 'تعذر إعادة تعيين كلمة المرور. قد تكون صلاحية الرابط انتهت.',
  [ErrorCodes.Auth.UserNotFound]: 'المستخدم غير موجود.',
  [ErrorCodes.Auth.ValidationFailed]: 'بيانات غير صالحة. راجع الحقول وحاول مرة أخرى.',

  [ErrorCodes.Settings.NotFound]: 'الإعدادات غير متاحة حاليًا.',

  [ErrorCodes.Availability.InvalidRange]: 'نطاق التواريخ غير صالح.',
  [ErrorCodes.Availability.RangeTooLarge]: 'النطاق الزمني المطلوب أكبر من المسموح.',
  [ErrorCodes.Availability.WindowNotFound]: 'الموعد المتاح غير موجود.',
  [ErrorCodes.Availability.BlockedDateNotFound]: 'الموعد غير المتاح غير موجود.',
  [ErrorCodes.Availability.BlockedRangeConflictsWithBookings]:
    'يتعارض الموعد غير المتاح مع حجوزات قائمة. قم بإلغاء الحجوزات المتعارضة أولًا ثم حاول مرة أخرى.',

  [ErrorCodes.Booking.SlotUnavailable]: 'هذا الموعد لم يعد متاحًا. يرجى اختيار موعد آخر.',
  [ErrorCodes.Booking.HoldCapExceeded]:
    'لديك الحد الأقصى من الحجوزات غير المؤكدة. قم بإلغاء أحدها من لوحتك ثم حاول مجددًا.',
  [ErrorCodes.Booking.EmailNotVerified]: 'يرجى تأكيد بريدك الإلكتروني قبل حجز جلسة.',
  [ErrorCodes.Booking.ContactPhoneRequired]: 'رقم الهاتف مطلوب للمكالمة الصوتية.',
  [ErrorCodes.Booking.ContactPhoneInvalid]: 'رقم الهاتف غير صالح. استخدم رقم موبايل مصري صحيح.',
  [ErrorCodes.Booking.NotFound]: 'لم يتم العثور على الحجز.',
  [ErrorCodes.Booking.Forbidden]: 'لا يمكنك عرض تعليمات هذا الحجز.',
  [ErrorCodes.Booking.InvalidStatus]: 'تعليمات الدفع غير متاحة لهذا الحجز.',

  [ErrorCodes.Cancellation.TooLate]: 'فات الأوان لطلب الإلغاء عبر الموقع — تواصل معنا على واتساب.',
  [ErrorCodes.Cancellation.ReopenExhausted]:
    'لا يمكن تقديم طلب إلغاء آخر عبر الموقع — تواصل معنا على واتساب.',
  [ErrorCodes.Cancellation.InvalidDecisionReason]: 'سبب رفض طلب الإلغاء غير صالح.',

  [ErrorCodes.Payment.NotFound]: 'لم يتم العثور على الدفع.',
  [ErrorCodes.Payment.Forbidden]: 'لا يمكنك الوصول إلى هذا الدفع.',
  [ErrorCodes.Payment.InvalidStatus]: 'لا يمكن رفع إيصال لهذا الحجز في حالته الحالية.',
  [ErrorCodes.Payment.UploadDeadlinePassed]: 'انتهت مهلة رفع الإيصال.',
  [ErrorCodes.Payment.InvalidReceiptFile]:
    'ملف الإيصال غير صالح. استخدم JPG أو PNG أو WebP أو PDF.',
  [ErrorCodes.Payment.ReceiptTooLarge]: 'حجم ملف الإيصال أكبر من 5 ميجابايت.',
  [ErrorCodes.Payment.InvalidMethod]: 'يرجى اختيار طريقة الدفع المستخدمة.',
  [ErrorCodes.Payment.NoPendingReceipt]: 'لا يوجد إيصال قيد المراجعة لهذا الحجز.',
  [ErrorCodes.Payment.InvalidDeclineReason]: 'سبب الرفض غير صالح.',
  [ErrorCodes.Payment.RefundNotDue]: 'لا يوجد استرداد مستحق لهذا الحجز.',
  [ErrorCodes.Payment.InvalidRefundReference]: 'مرجع الاسترداد مطلوب.',

  [ErrorCodes.General.Validation]: 'توجد أخطاء في البيانات المدخلة. يرجى مراجعة الحقول.',
  [ErrorCodes.General.Unexpected]: 'حدث خطأ غير متوقع. حاول مرة أخرى.',
  [ErrorCodes.General.Forbidden]: 'لا تملك صلاحية القيام بهذا الإجراء.',

  [ErrorCodes.Errors.NotFound]: 'الخطأ المطلوب غير موجود.',
};
