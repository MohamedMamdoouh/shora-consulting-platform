import { FormControl } from '@angular/forms';
import { getAuthFieldError } from './auth-field-error.util';

describe('getAuthFieldError', () => {
  it('returns null when control is untouched', () => {
    const control = new FormControl('', { validators: [] });
    control.setErrors({ required: true });

    expect(getAuthFieldError(control, 'password')).toBeNull();
  });

  it('returns required message after touch', () => {
    const control = new FormControl('');
    control.markAsTouched();
    control.setErrors({ required: true });

    expect(getAuthFieldError(control, 'password')).toBe('كلمة المرور مطلوب');
  });

  it('returns minlength message for password fields', () => {
    const control = new FormControl('short');
    control.markAsTouched();
    control.setErrors({ minlength: { requiredLength: 8, actualLength: 5 } });

    expect(getAuthFieldError(control, 'newPassword')).toBe('يجب أن تكون كلمة المرور 8 أحرف على الأقل');
  });

  it('returns email format message', () => {
    const control = new FormControl('not-an-email');
    control.markAsTouched();
    control.setErrors({ email: true });

    expect(getAuthFieldError(control, 'email')).toBe('أدخل بريداً إلكترونياً صالحاً');
  });
});
