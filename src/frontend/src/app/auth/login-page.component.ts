import { AfterViewInit, Component, inject, OnInit } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { finalize } from 'rxjs';
import { environment } from '../../environments/environment';
import { AuthService } from '../core/auth/auth.service';
import { readApiError } from '../core/api/api-error.util';
import { getAuthFieldError } from '../core/forms/auth-field-error.util';

declare global {
  interface Window {
    google?: {
      accounts: {
        id: {
          initialize: (config: {
            client_id: string;
            callback: (response: { credential: string }) => void;
          }) => void;
          renderButton: (
            element: HTMLElement,
            config: { theme: string; size: string; width: number },
          ) => void;
        };
      };
    };
  }
}

@Component({
  selector: 'app-login-page',
  imports: [ReactiveFormsModule, RouterLink],
  templateUrl: './login-page.component.html',
  styleUrl: './login-page.component.scss',
})
export class LoginPageComponent implements OnInit, AfterViewInit {
  private readonly fb = inject(FormBuilder);
  private readonly auth = inject(AuthService);
  private readonly route = inject(ActivatedRoute);

  errorMessage = '';
  infoMessage = '';
  isSubmitting = false;

  readonly getFieldError = getAuthFieldError;

  readonly form = this.fb.nonNullable.group({
    email: ['', [Validators.required, Validators.email]],
    password: ['', [Validators.required]],
  });

  ngOnInit(): void {
    if (this.route.snapshot.queryParamMap.get('reason') === 'sessionExpired') {
      this.infoMessage = 'انتهت جلستك. يرجى تسجيل الدخول مرة أخرى.';
    }
  }

  ngAfterViewInit(): void {
    this.renderGoogleButton();
  }

  submit(): void {
    if (this.isSubmitting) {
      return;
    }

    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    const { email, password } = this.form.getRawValue();
    this.errorMessage = '';
    this.isSubmitting = true;

    this.auth
      .login(email, password)
      .pipe(
        finalize(() => {
          this.isSubmitting = false;
        }),
      )
      .subscribe({
        next: (response) => {
          this.auth.redirectAfterLogin(response.role);
        },
        error: (err) => {
          this.errorMessage = readApiError(err, 'البريد الإلكتروني أو كلمة المرور غير صحيحة.');
        },
      });
  }

  private renderGoogleButton(): void {
    if (!environment.googleClientId) {
      return;
    }

    const container = document.getElementById('google-signin-button');
    if (!container || !window.google?.accounts?.id) {
      return;
    }

    window.google.accounts.id.initialize({
      client_id: environment.googleClientId,
      callback: (response) => {
        this.auth.googleSignIn(response.credential).subscribe({
          next: (authResponse) => this.auth.redirectAfterLogin(authResponse.role),
          error: (err) => {
            this.errorMessage = readApiError(err, 'تعذر تسجيل الدخول عبر جوجل.');
          },
        });
      },
    });

    window.google.accounts.id.renderButton(container, {
      theme: 'outline',
      size: 'large',
      width: 280,
    });
  }
}
