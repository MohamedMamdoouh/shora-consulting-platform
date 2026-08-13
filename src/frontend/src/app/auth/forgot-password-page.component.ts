import { Component, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { AuthService } from '../core/auth/auth.service';
import { readApiError } from '../core/api/api-error.util';
import { getAuthFieldError } from '../core/forms/auth-field-error.util';

@Component({
  selector: 'app-forgot-password-page',
  imports: [ReactiveFormsModule, RouterLink],
  templateUrl: './forgot-password-page.component.html',
  styleUrl: './forgot-password-page.component.scss',
})
export class ForgotPasswordPageComponent {
  private readonly fb = inject(FormBuilder);
  private readonly auth = inject(AuthService);

  readonly submitted = signal(false);
  readonly isSubmitting = signal(false);
  readonly errorMessage = signal('');

  readonly getFieldError = getAuthFieldError;

  readonly form = this.fb.nonNullable.group({
    email: ['', [Validators.required, Validators.email]],
  });

  async submit(): Promise<void> {
    if (this.isSubmitting()) {
      return;
    }

    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    this.errorMessage.set('');
    this.isSubmitting.set(true);

    try {
      await firstValueFrom(this.auth.forgotPassword(this.form.controls.email.value));
      this.submitted.set(true);
    } catch (err) {
      this.errorMessage.set(readApiError(err, 'تعذر إرسال رابط إعادة التعيين. حاول مرة أخرى.'));
    } finally {
      this.isSubmitting.set(false);
    }
  }
}
