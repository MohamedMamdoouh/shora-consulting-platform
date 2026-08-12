import { AbstractControl } from '@angular/forms';

const fieldLabels: Record<string, string> = {
  email: 'البريد الإلكتروني',
  password: 'كلمة المرور',
  newPassword: 'كلمة المرور',
};

export function getAuthFieldError(control: AbstractControl | null, fieldName: string): string | null {
  if (!control?.errors || !control.touched) {
    return null;
  }

  const label = fieldLabels[fieldName] ?? 'هذا الحقل';

  if (control.errors['required']) {
    return `${label} مطلوب`;
  }

  if (control.errors['email']) {
    return 'أدخل بريدًا إلكترونيًا صالحًا';
  }

  if (control.errors['minlength']) {
    return `يجب أن تكون ${label} 8 أحرف على الأقل`;
  }

  return null;
}
