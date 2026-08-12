import { Component, inject } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { AuthService } from '../core/auth/auth.service';
import { readApiError } from '../core/api/api-error.util';
import { getAuthFieldError } from '../core/forms/auth-field-error.util';
import { BrandLogoComponent } from '../shared/components/brand-logo.component';

@Component({
  selector: 'app-forgot-password-page',
  imports: [BrandLogoComponent, ReactiveFormsModule, RouterLink],
  templateUrl: './forgot-password-page.component.html',
  styleUrl: './forgot-password-page.component.scss',
})
export class ForgotPasswordPageComponent {
  private readonly fb = inject(FormBuilder);
  private readonly auth = inject(AuthService);

  submitted = false;
  isSubmitting = false;
  errorMessage = '';

  readonly getFieldError = getAuthFieldError;

  readonly form = this.fb.nonNullable.group({
    email: ['', [Validators.required, Validators.email]],
  });

  async submit(): Promise<void> {
    if (this.isSubmitting) {
      return;
    }

    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    this.errorMessage = '';
    this.isSubmitting = true;

    try {
      await firstValueFrom(this.auth.forgotPassword(this.form.controls.email.value));
      this.submitted = true;
    } catch (err) {
      this.errorMessage = readApiError(err, 'تعذر إرسال رابط إعادة التعيين. حاول مرة أخرى.');
    } finally {
      this.isSubmitting = false;
    }
  }
}
