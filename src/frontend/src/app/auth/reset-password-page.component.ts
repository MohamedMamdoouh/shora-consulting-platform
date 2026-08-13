import { Component, inject, OnInit, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { AuthService } from '../core/auth/auth.service';
import { readApiError } from '../core/api/api-error.util';
import { getAuthFieldError } from '../core/forms/auth-field-error.util';

@Component({
  selector: 'app-reset-password-page',
  imports: [ReactiveFormsModule, RouterLink],
  templateUrl: './reset-password-page.component.html',
  styleUrl: './reset-password-page.component.scss'
})
export class ResetPasswordPageComponent implements OnInit {
  private readonly fb = inject(FormBuilder);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly auth = inject(AuthService);

  email = '';
  token = '';
  readonly errorMessage = signal('');
  readonly success = signal(false);
  readonly isSubmitting = signal(false);

  readonly getFieldError = getAuthFieldError;

  readonly form = this.fb.nonNullable.group({
    newPassword: ['', [Validators.required, Validators.minLength(8)]]
  });

  ngOnInit(): void {
    this.email = this.route.snapshot.queryParamMap.get('email') ?? '';
    this.token = this.route.snapshot.queryParamMap.get('token') ?? '';
  }

  async submit(): Promise<void> {
    if (this.isSubmitting()) {
      return;
    }

    if (this.form.invalid || !this.email || !this.token) {
      this.form.markAllAsTouched();
      return;
    }

    this.errorMessage.set('');
    this.isSubmitting.set(true);

    try {
      await firstValueFrom(
        this.auth.resetPassword(this.email, this.token, this.form.controls.newPassword.value),
      );
      this.success.set(true);
      await this.router.navigate(['/auth/login']);
    } catch (err) {
      this.errorMessage.set(readApiError(err, 'تعذر إعادة تعيين كلمة المرور. قد تكون صلاحية الرابط انتهت.'));
    } finally {
      this.isSubmitting.set(false);
    }
  }
}
