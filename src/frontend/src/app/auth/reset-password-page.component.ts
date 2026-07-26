import { Component, inject, OnInit } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { finalize } from 'rxjs';
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
  errorMessage = '';
  success = false;
  isSubmitting = false;

  readonly getFieldError = getAuthFieldError;

  readonly form = this.fb.nonNullable.group({
    newPassword: ['', [Validators.required, Validators.minLength(8)]]
  });

  ngOnInit(): void {
    this.email = this.route.snapshot.queryParamMap.get('email') ?? '';
    this.token = this.route.snapshot.queryParamMap.get('token') ?? '';
  }

  submit(): void {
    if (this.isSubmitting) {
      return;
    }

    if (this.form.invalid || !this.email || !this.token) {
      this.form.markAllAsTouched();
      return;
    }

    this.errorMessage = '';
    this.isSubmitting = true;

    this.auth
      .resetPassword(this.email, this.token, this.form.controls.newPassword.value)
      .pipe(finalize(() => {
        this.isSubmitting = false;
      }))
      .subscribe({
        next: () => {
          this.success = true;
          void this.router.navigate(['/auth/login']);
        },
        error: (err) => {
          this.errorMessage = readApiError(err, 'تعذر إعادة تعيين كلمة المرور. قد يكون الرابط منتهياً.');
        },
      });
  }
}
