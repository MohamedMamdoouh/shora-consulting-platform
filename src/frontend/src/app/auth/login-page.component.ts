import { Component, inject, OnInit, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { AuthService } from '../core/auth/auth.service';
import { readApiError } from '../core/api/api-error.util';
import { getAuthFieldError } from '../core/forms/auth-field-error.util';

@Component({
  selector: 'app-login-page',
  imports: [ReactiveFormsModule, RouterLink],
  templateUrl: './login-page.component.html',
  styleUrl: './login-page.component.scss',
})
export class LoginPageComponent implements OnInit {
  private readonly fb = inject(FormBuilder);
  private readonly auth = inject(AuthService);
  private readonly route = inject(ActivatedRoute);

  readonly errorMessage = signal('');
  readonly infoMessage = signal('');
  readonly isSubmitting = signal(false);
  readonly returnUrl = this.route.snapshot.queryParamMap.get('returnUrl');
  readonly authQueryParams = this.returnUrl ? { returnUrl: this.returnUrl } : {};

  readonly getFieldError = getAuthFieldError;

  readonly form = this.fb.nonNullable.group({
    email: ['', [Validators.required, Validators.email]],
    password: ['', [Validators.required]],
  });

  ngOnInit(): void {
    if (this.route.snapshot.queryParamMap.get('reason') === 'sessionExpired') {
      this.infoMessage.set('انتهت جلستك. يرجى تسجيل الدخول مرة أخرى.');
    }
  }

  async submit(): Promise<void> {
    if (this.isSubmitting()) {
      return;
    }

    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    const { email, password } = this.form.getRawValue();
    this.errorMessage.set('');
    this.isSubmitting.set(true);

    try {
      const response = await firstValueFrom(this.auth.login(email, password));
      await this.auth.redirectAfterLogin(response.role, this.returnUrl);
    } catch (err) {
      this.errorMessage.set(readApiError(err, 'البريد الإلكتروني أو كلمة المرور غير صحيحة.'));
    } finally {
      this.isSubmitting.set(false);
    }
  }
}
