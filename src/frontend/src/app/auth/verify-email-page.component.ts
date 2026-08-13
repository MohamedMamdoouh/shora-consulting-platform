import { Component, inject, OnInit, signal } from '@angular/core';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { firstValueFrom } from 'rxjs';
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

  readonly status = signal<'loading' | 'success' | 'error'>('loading');
  readonly message = signal('');
  private isVerifying = false;

  ngOnInit(): void {
    void this.verifyFromQueryParams();
  }

  private async verifyFromQueryParams(): Promise<void> {
    if (this.isVerifying || this.status() !== 'loading') {
      return;
    }

    const email = this.route.snapshot.queryParamMap.get('email');
    const token = this.route.snapshot.queryParamMap.get('token');

    if (!email || !token) {
      this.status.set('error');
      this.message.set('رابط التحقق غير صالح.');
      return;
    }

    this.isVerifying = true;

    try {
      const response = await firstValueFrom(this.auth.verifyEmail(email, token));
      const alreadyVerified = response.message.toLowerCase().includes('already');
      this.completeSuccess(
        alreadyVerified
          ? 'حسابك مؤكد بالفعل. سيتم تحويلك لتسجيل الدخول.'
          : 'تم تأكيد بريدك الإلكتروني بنجاح. سيتم تحويلك لتسجيل الدخول.',
      );
    } catch (err) {
      this.status.set('error');
      this.message.set(
        readApiErrorCode(err) === ErrorCodes.Auth.VerificationFailed
          ? 'رابط التحقق منتهي أو غير صالح.'
          : readApiError(err, 'تعذر تأكيد البريد الإلكتروني. حاول مرة أخرى.'),
      );
    } finally {
      this.isVerifying = false;
    }
  }

  private completeSuccess(message: string): void {
    this.status.set('success');
    this.message.set(message);

    setTimeout(() => {
      void this.router.navigate(['/auth/login']);
    }, 2000);
  }
}
