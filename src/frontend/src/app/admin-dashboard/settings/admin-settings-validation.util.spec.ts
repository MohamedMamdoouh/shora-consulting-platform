import { FormControl } from '@angular/forms';
import { describe, expect, it } from 'vitest';
import {
  ADMIN_SETTINGS_LIMITS,
  consultantWhatsAppValidators,
  getAdminSettingsFieldError,
  paymentInstructionsValidators,
  sessionPriceValidators,
  vodafoneCashValidators,
} from './admin-settings-validation.util';

describe('admin settings validation utilities', () => {
  it('accepts positive session prices with at most two decimal places', () => {
    const valid = new FormControl('125.50', sessionPriceValidators());
    const tooPrecise = new FormControl('125.555', sessionPriceValidators());
    const zero = new FormControl(0, sessionPriceValidators());

    expect(valid.errors).toBeNull();
    expect(tooPrecise.errors).toEqual({ decimals: true });
    expect(zero.errors).toEqual({ min: true });
  });

  it('validates consultant WhatsApp as E.164 and Vodafone Cash as Egyptian mobile number', () => {
    const consultantWhatsApp = new FormControl('+201012345678', consultantWhatsAppValidators());
    const localWhatsApp = new FormControl('01012345678', consultantWhatsAppValidators());
    const vodafoneCash = new FormControl('01012345678', vodafoneCashValidators());
    const unsupportedVodafoneCarrier = new FormControl('+201712345678', vodafoneCashValidators());

    expect(consultantWhatsApp.errors).toBeNull();
    expect(localWhatsApp.errors).toEqual({ e164: true });
    expect(vodafoneCash.errors).toBeNull();
    expect(unsupportedVodafoneCarrier.errors).toEqual({ egyptMobile: true });
  });

  it('keeps payment instructions within the server-enforced length limit', () => {
    const withinLimit = new FormControl(
      'x'.repeat(ADMIN_SETTINGS_LIMITS.maxPaymentInstructionsLength),
      paymentInstructionsValidators(),
    );
    const beyondLimit = new FormControl(
      'x'.repeat(ADMIN_SETTINGS_LIMITS.maxPaymentInstructionsLength + 1),
      paymentInstructionsValidators(),
    );

    expect(withinLimit.errors).toBeNull();
    expect(beyondLimit.errors?.['maxlength']).toMatchObject({
      requiredLength: ADMIN_SETTINGS_LIMITS.maxPaymentInstructionsLength,
    });
  });

  it('only surfaces field errors after touch and gives server errors precedence', () => {
    const control = new FormControl('');
    control.setErrors({ required: true });

    expect(getAdminSettingsFieldError(control, 'sessionPrice')).toBeNull();

    control.markAsTouched();
    expect(getAdminSettingsFieldError(control, 'sessionPrice')).not.toBeNull();

    control.setErrors({ server: 'Server says no', required: true });
    expect(getAdminSettingsFieldError(control, 'sessionPrice')).toBe('Server says no');
  });

  it('uses field-specific boundary messages for high-risk numeric settings', () => {
    const receiptWindow = new FormControl(0);
    receiptWindow.setErrors({ min: true });
    receiptWindow.markAsTouched();

    const paymentInstructions = new FormControl('');
    paymentInstructions.setErrors({ maxlength: true });
    paymentInstructions.markAsTouched();

    expect(getAdminSettingsFieldError(receiptWindow, 'receiptUploadWindowMinutes')).toContain(
      String(ADMIN_SETTINGS_LIMITS.minReceiptUploadWindowMinutes),
    );
    expect(getAdminSettingsFieldError(paymentInstructions, 'paymentInstructions')).toContain(
      String(ADMIN_SETTINGS_LIMITS.maxPaymentInstructionsLength),
    );
  });
});
