import { AbstractControl, ValidationErrors, ValidatorFn, Validators } from '@angular/forms';

const MIN_SESSION_DURATION = 30;
const MAX_SESSION_DURATION = 240;
const MIN_RECEIPT_WINDOW = 5;
const MAX_PAYMENT_INSTRUCTIONS = 2000;
const E164_PATTERN = /^\+[1-9]\d{7,14}$/;
const EGYPT_MOBILE_PATTERN = /^(?:\+20|0)?1[0125]\d{8}$/;

export const ADMIN_SETTINGS_LIMITS = {
  minSessionDurationMinutes: MIN_SESSION_DURATION,
  maxSessionDurationMinutes: MAX_SESSION_DURATION,
  minReceiptUploadWindowMinutes: MIN_RECEIPT_WINDOW,
  maxPaymentInstructionsLength: MAX_PAYMENT_INSTRUCTIONS,
} as const;

export function sessionPriceValidators(): ValidatorFn[] {
  return [Validators.required, sessionPriceValidator];
}

export function sessionDurationValidators(): ValidatorFn[] {
  return [
    Validators.required,
    Validators.min(MIN_SESSION_DURATION),
    Validators.max(MAX_SESSION_DURATION),
  ];
}

export function nonNegativeIntValidators(): ValidatorFn[] {
  return [Validators.required, Validators.min(0)];
}

export function receiptUploadWindowValidators(): ValidatorFn[] {
  return [Validators.required, Validators.min(MIN_RECEIPT_WINDOW)];
}

export function consultantWhatsAppValidators(): ValidatorFn[] {
  return [Validators.required, e164PhoneValidator];
}

export function vodafoneCashValidators(): ValidatorFn[] {
  return [Validators.required, egyptianMobileValidator];
}

export function instaPayHandleValidators(): ValidatorFn[] {
  return [Validators.required, Validators.maxLength(100)];
}

export function paymentInstructionsValidators(): ValidatorFn[] {
  return [Validators.maxLength(MAX_PAYMENT_INSTRUCTIONS)];
}

export function getAdminSettingsFieldError(
  control: AbstractControl | null,
  field: string,
): string | null {
  if (!control?.errors || !control.touched) {
    return null;
  }

  if (control.errors['server']) {
    return control.errors['server'] as string;
  }

  if (control.errors['required']) {
    return 'هذا الحقل مطلوب.';
  }

  if (control.errors['min']) {
    switch (field) {
      case 'sessionPrice':
        return 'يجب أن يكون السعر أكبر من صفر.';
      case 'sessionDurationMinutes':
        return `المدة يجب أن تكون بين ${MIN_SESSION_DURATION} و${MAX_SESSION_DURATION} دقيقة.`;
      case 'bufferMinutes':
        return 'وقت الفاصل يجب أن يكون صفرًا أو أكثر.';
      case 'receiptUploadWindowMinutes':
        return `مهلة رفع الإيصال يجب أن تكون ${MIN_RECEIPT_WINDOW} دقائق على الأقل.`;
      case 'cancellationRequestAutoDeclineHours':
        return 'مهلة رفض طلب الإلغاء يجب أن تكون صفرًا أو أكثر.';
      default:
        return 'القيمة أقل من الحد المسموح.';
    }
  }

  if (control.errors['max']) {
    if (field === 'sessionDurationMinutes') {
      return `المدة يجب أن تكون بين ${MIN_SESSION_DURATION} و${MAX_SESSION_DURATION} دقيقة.`;
    }

    return 'القيمة أكبر من الحد المسموح.';
  }

  if (control.errors['maxlength']) {
    return field === 'paymentInstructions'
      ? `الملاحظة يجب ألا تتجاوز ${MAX_PAYMENT_INSTRUCTIONS} حرفًا.`
      : 'النص أطول من المسموح.';
  }

  if (control.errors['decimals']) {
    return 'السعر يجب ألا يحتوي على أكثر من منزلتين عشريتين.';
  }

  if (control.errors['e164']) {
    return 'رقم واتساب غير صالح. استخدم صيغة E.164 مثل +201012345678.';
  }

  if (control.errors['egyptMobile']) {
    return 'رقم Vodafone Cash غير صالح. استخدم رقم موبايل مصري مثل 01012345678.';
  }

  return null;
}

function sessionPriceValidator(control: AbstractControl): ValidationErrors | null {
  const value = control.value;

  if (value === null || value === undefined || value === '') {
    return { required: true };
  }

  const numeric = Number(value);
  if (!Number.isFinite(numeric) || numeric <= 0) {
    return { min: true };
  }

  if (decimalPlaces(numeric) > 2) {
    return { decimals: true };
  }

  return null;
}

function e164PhoneValidator(control: AbstractControl): ValidationErrors | null {
  const value = String(control.value ?? '').trim();
  return E164_PATTERN.test(value) ? null : { e164: true };
}

function egyptianMobileValidator(control: AbstractControl): ValidationErrors | null {
  const value = String(control.value ?? '').trim();
  return EGYPT_MOBILE_PATTERN.test(value) ? null : { egyptMobile: true };
}

function decimalPlaces(value: number): number {
  const parts = value.toString().split('.');
  return parts.length > 1 ? parts[1].length : 0;
}
