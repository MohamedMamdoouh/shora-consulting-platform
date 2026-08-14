import { Component, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { AuthService } from '../core/auth/auth.service';
import { readApiError } from '../core/api/api-error.util';
import { getAuthFieldError } from '../core/forms/auth-field-error.util';

@Component({
  selector: 'app-signup-page',
  imports: [ReactiveFormsModule, RouterLink],
  templateUrl: './signup-page.component.html',
  styleUrl: './signup-page.component.scss',
})
export class SignupPageComponent {
  private readonly fb = inject(FormBuilder);
  private readonly auth = inject(AuthService);
  private readonly route = inject(ActivatedRoute);

  readonly errorMessage = signal('');
  readonly isSubmitting = signal(false);
  readonly returnUrl = this.route.snapshot.queryParamMap.get('returnUrl');
  readonly authQueryParams = this.returnUrl ? { returnUrl: this.returnUrl } : {};

  readonly getFieldError = getAuthFieldError;

  readonly form = this.fb.nonNullable.group({
    email: ['', [Validators.required, Validators.email]],
    password: ['', [Validators.required, Validators.minLength(8)]],
    displayName: [''],
  });

  async submit(): Promise<void> {
    if (this.isSubmitting()) {
      return;
    }

    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    const { email, password, displayName } = this.form.getRawValue();
    this.errorMessage.set('');
    this.isSubmitting.set(true);

    try {
      const response = await firstValueFrom(
        this.auth.signup(email, password, displayName || undefined),
      );
      await this.auth.redirectAfterLogin(response.role, this.returnUrl);
    } catch (err) {
      this.errorMessage.set(readApiError(err, 'تعذر إنشاء الحساب. راجع البيانات وحاول مرة أخرى.'));
    } finally {
      this.isSubmitting.set(false);
    }
  }
}
