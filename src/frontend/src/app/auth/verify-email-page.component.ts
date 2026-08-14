import { Component, inject, OnInit, signal } from '@angular/core';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { AuthService } from '../core/auth/auth.service';
import { readApiError } from '../core/api/api-error.util';
import { ConfirmDialogService } from '../core/ui/confirm-dialog.service';

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
  private readonly confirmDialog = inject(ConfirmDialogService);

  readonly status = signal<'loading' | 'success' | 'error'>('loading');
  readonly message = signal('');

  ngOnInit(): void {
    void this.verifyFromQueryParams();
  }

  private async verifyFromQueryParams(): Promise<void> {
    const email = this.route.snapshot.queryParamMap.get('email');
    const token = this.route.snapshot.queryParamMap.get('token');

    if (!email || !token) {
      this.status.set('error');
      this.message.set('رابط التحقق غير صالح.');
      return;
    }

    try {
      const response = await firstValueFrom(this.auth.verifyEmail(email, token));

      if (!('accessToken' in response)) {
        try {
          await firstValueFrom(this.auth.refresh());
        } catch {
          // User may have opened the link on another device without an active session.
        }
      }

      if (this.auth.isAuthenticated()) {
        await this.auth.syncCurrentUser();
      }

      const alreadyVerified =
        'message' in response && response.message.toLowerCase().includes('already');
      await this.completeSuccess(
        alreadyVerified ? 'حسابك مؤكد بالفعل.' : 'تم تأكيد بريدك الإلكتروني بنجاح.',
      );
    } catch (err) {
      const errorMessage = readApiError(err, 'تعذر تأكيد البريد الإلكتروني. حاول مرة أخرى.');
      this.status.set('error');
      this.message.set(errorMessage);
      await this.confirmDialog.result({
        message: errorMessage,
        variant: 'danger',
      });
    }
  }

  private async completeSuccess(message: string): Promise<void> {
    this.status.set('success');
    this.message.set(message);

    await this.confirmDialog.result({
      message,
      onComplete: () => this.redirectAfterSuccess(),
    });
  }

  private async redirectAfterSuccess(): Promise<void> {
    const returnUrl = this.auth.sanitizeReturnUrl(
      this.route.snapshot.queryParamMap.get('returnUrl'),
    );
    const user = this.auth.getCurrentUser();

    if (this.auth.isAuthenticated() && user?.emailConfirmed) {
      if (returnUrl) {
        await this.router.navigateByUrl(returnUrl);
        return;
      }

      await this.auth.redirectAfterLogin(user.role);
      return;
    }

    await this.router.navigate(['/auth/login'], {
      queryParams: returnUrl ? { returnUrl } : undefined,
    });
  }
}
