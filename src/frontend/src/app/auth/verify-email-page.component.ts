import { Component, inject, OnInit } from '@angular/core';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { finalize } from 'rxjs';
import { ErrorCodes } from '@contracts/error-codes';
import { AuthService } from '../core/auth/auth.service';
import { readApiError, readApiErrorCode } from '../core/api/api-error.util';

@Component({
  selector: 'app-verify-email-page',
  imports: [RouterLink],
  templateUrl: './verify-email-page.component.html',
  styleUrl: './verify-email-page.component.scss',
})
export class VerifyEmailPageComponent implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly auth = inject(AuthService);

  status: 'loading' | 'success' | 'error' = 'loading';
  message = '';
  private isVerifying = false;

  ngOnInit(): void {
    this.verifyFromQueryParams();
  }

  private verifyFromQueryParams(): void {
    if (this.isVerifying || this.status !== 'loading') {
      return;
    }

    const email = this.route.snapshot.queryParamMap.get('email');
    const token = this.route.snapshot.queryParamMap.get('token');

    if (!email || !token) {
      this.status = 'error';
      this.message = 'رابط التحقق غير صالح.';
      return;
    }

    this.isVerifying = true;

    this.auth
      .verifyEmail(email, token)
      .pipe(
        finalize(() => {
          this.isVerifying = false;
        }),
      )
      .subscribe({
        next: (response) => {
          const alreadyVerified = response.message.toLowerCase().includes('already');
          this.completeSuccess(
            alreadyVerified
              ? 'حسابك مؤكد بالفعل. سيتم تحويلك لتسجيل الدخول.'
              : 'تم تأكيد بريدك الإلكتروني بنجاح. سيتم تحويلك لتسجيل الدخول.',
          );
        },
        error: (err) => {
          this.status = 'error';
          this.message =
            readApiErrorCode(err) === ErrorCodes.Auth.VerificationFailed
              ? 'رابط التحقق منتهي أو غير صالح.'
              : readApiError(err, 'تعذر تأكيد البريد الإلكتروني. حاول مرة أخرى.');
        },
      });
  }

  private completeSuccess(message: string): void {
    this.status = 'success';
    this.message = message;

    setTimeout(() => {
      void this.router.navigate(['/auth/login']);
    }, 2000);
  }
}
