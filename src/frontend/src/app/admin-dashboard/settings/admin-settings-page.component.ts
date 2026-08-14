import { Component, inject, OnInit, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule } from '@angular/forms';
import { HttpErrorResponse } from '@angular/common/http';
import { AdminSettings, UpdateAdminSettingsRequest } from '@contracts/settings';
import { firstValueFrom } from 'rxjs';
import { readApiError, readValidationErrors } from '../../core/api/api-error.util';
import { settingsPublicRequest } from '../../core/api/cache.config';
import { ApiCacheService } from '../../core/api/api-cache.service';
import { AdminSettingsService } from '../../core/admin/admin-settings.service';
import { APP_COPY } from '../../core/i18n/app-copy.constants';
import { ConfirmDialogService } from '../../core/ui/confirm-dialog.service';
import { environment } from '../../../environments/environment';
import {
  consultantWhatsAppValidators,
  getAdminSettingsFieldError,
  instaPayHandleValidators,
  nonNegativeIntValidators,
  paymentInstructionsValidators,
  receiptUploadWindowValidators,
  sessionDurationValidators,
  sessionPriceValidators,
  vodafoneCashValidators,
} from './admin-settings-validation.util';

type PageState =
  | { status: 'loading' }
  | { status: 'error'; message: string }
  | { status: 'ready'; receiptRetentionMonths: number };

@Component({
  selector: 'app-admin-settings-page',
  imports: [ReactiveFormsModule],
  templateUrl: './admin-settings-page.component.html',
  styleUrl: './admin-settings-page.component.scss',
})
export class AdminSettingsPageComponent implements OnInit {
  private readonly fb = inject(FormBuilder);
  private readonly adminSettingsService = inject(AdminSettingsService);
  private readonly apiCache = inject(ApiCacheService);
  private readonly confirmDialog = inject(ConfirmDialogService);

  private readonly copy = APP_COPY;

  readonly pageState = signal<PageState>({ status: 'loading' });
  readonly errorMessage = signal('');
  readonly isSubmitting = signal(false);

  readonly getFieldError = getAdminSettingsFieldError;

  readonly form = this.fb.nonNullable.group({
    sessionPrice: this.fb.nonNullable.control(0, sessionPriceValidators()),
    sessionDurationMinutes: this.fb.nonNullable.control(60, sessionDurationValidators()),
    bufferMinutes: this.fb.nonNullable.control(15, nonNegativeIntValidators()),
    receiptUploadWindowMinutes: this.fb.nonNullable.control(60, receiptUploadWindowValidators()),
    cancellationRequestAutoDeclineHours: this.fb.nonNullable.control(1, nonNegativeIntValidators()),
    consultantWhatsAppNumber: this.fb.nonNullable.control('', consultantWhatsAppValidators()),
    vodafoneCashNumber: this.fb.nonNullable.control('', vodafoneCashValidators()),
    instaPayHandle: this.fb.nonNullable.control('', instaPayHandleValidators()),
    paymentInstructions: this.fb.control<string | null>(null, paymentInstructionsValidators()),
  });

  ngOnInit(): void {
    void this.loadSettings();
  }

  async loadSettings(): Promise<void> {
    this.pageState.set({ status: 'loading' });
    this.errorMessage.set('');

    try {
      const settings = await firstValueFrom(this.adminSettingsService.getSettings());
      this.patchForm(settings);
      this.pageState.set({
        status: 'ready',
        receiptRetentionMonths: settings.receiptRetentionMonths,
      });
    } catch (error) {
      this.pageState.set({
        status: 'error',
        message: readApiError(error, 'تعذر تحميل الإعدادات. حاول مرة أخرى.'),
      });
    }
  }

  async submit(): Promise<void> {
    if (this.isSubmitting() || this.pageState().status !== 'ready') {
      return;
    }

    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    const confirmed = await this.confirmDialog.confirm({
      title: this.copy.admin.dialog.saveSettingsTitle,
      message: this.copy.admin.dialog.saveSettingsMessage,
      confirmLabel: this.copy.admin.dialog.saveSettingsAction,
    });

    if (!confirmed) {
      return;
    }

    this.errorMessage.set('');
    this.isSubmitting.set(true);

    try {
      const payload = this.buildUpdateRequest();
      const updated = await firstValueFrom(this.adminSettingsService.updateSettings(payload));
      this.patchForm(updated);
      this.pageState.set({
        status: 'ready',
        receiptRetentionMonths: updated.receiptRetentionMonths,
      });
      this.apiCache.invalidate(settingsPublicRequest(environment.apiBaseUrl).url);
    } catch (error) {
      if (this.applyServerValidationErrors(error)) {
        return;
      }

      this.errorMessage.set(
        readApiError(error, 'تعذر حفظ الإعدادات. راجع البيانات وحاول مرة أخرى.'),
      );
      return;
    } finally {
      this.isSubmitting.set(false);
    }

    await this.confirmDialog.result({
      title: this.copy.admin.dialog.settingsSavedTitle,
      message: this.copy.admin.dialog.settingsSavedMessage,
    });
  }

  private patchForm(settings: AdminSettings): void {
    this.form.reset({
      sessionPrice: settings.sessionPrice,
      sessionDurationMinutes: settings.sessionDurationMinutes,
      bufferMinutes: settings.bufferMinutes,
      receiptUploadWindowMinutes: settings.receiptUploadWindowMinutes,
      cancellationRequestAutoDeclineHours: settings.cancellationRequestAutoDeclineHours,
      consultantWhatsAppNumber: settings.consultantWhatsAppNumber,
      vodafoneCashNumber: settings.vodafoneCashNumber,
      instaPayHandle: settings.instaPayHandle,
      paymentInstructions: settings.paymentInstructions ?? null,
    });
  }

  private buildUpdateRequest(): UpdateAdminSettingsRequest {
    const raw = this.form.getRawValue();
    const instructions = raw.paymentInstructions?.trim();

    return {
      sessionPrice: Number(raw.sessionPrice),
      sessionDurationMinutes: Number(raw.sessionDurationMinutes),
      bufferMinutes: Number(raw.bufferMinutes),
      receiptUploadWindowMinutes: Number(raw.receiptUploadWindowMinutes),
      cancellationRequestAutoDeclineHours: Number(raw.cancellationRequestAutoDeclineHours),
      consultantWhatsAppNumber: raw.consultantWhatsAppNumber.trim(),
      vodafoneCashNumber: raw.vodafoneCashNumber.trim(),
      instaPayHandle: raw.instaPayHandle.trim(),
      paymentInstructions: instructions ? instructions : null,
    };
  }

  private applyServerValidationErrors(error: unknown): boolean {
    const fieldErrors = readValidationErrors(error);
    if (!fieldErrors) {
      return false;
    }

    for (const [field, messages] of Object.entries(fieldErrors)) {
      const control = this.form.get(field);
      if (!control || messages.length === 0) {
        continue;
      }

      control.setErrors({ ...control.errors, server: true });
      control.markAsTouched();
    }

    if (error instanceof HttpErrorResponse) {
      this.errorMessage.set(readApiError(error, 'تحقق من الحقول المظللة.'));
    }

    return true;
  }
}
