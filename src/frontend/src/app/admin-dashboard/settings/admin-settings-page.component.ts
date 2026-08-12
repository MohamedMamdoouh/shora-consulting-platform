import { Component, inject, OnInit } from '@angular/core';
import { FormBuilder, ReactiveFormsModule } from '@angular/forms';
import { HttpErrorResponse } from '@angular/common/http';
import { AdminSettings, UpdateAdminSettingsRequest } from '@contracts/settings';
import { firstValueFrom } from 'rxjs';
import { readApiError, readValidationErrors } from '../../core/api/api-error.util';
import { settingsPublicRequest } from '../../core/api/cache.config';
import { ApiCacheService } from '../../core/api/api-cache.service';
import { AdminSettingsService } from '../../core/admin/admin-settings.service';
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

  pageState: PageState = { status: 'loading' };
  successMessage = '';
  errorMessage = '';
  isSubmitting = false;

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
    this.pageState = { status: 'loading' };
    this.successMessage = '';
    this.errorMessage = '';

    try {
      const settings = await firstValueFrom(this.adminSettingsService.getSettings());
      this.patchForm(settings);
      this.pageState = { status: 'ready', receiptRetentionMonths: settings.receiptRetentionMonths };
    } catch (error) {
      this.pageState = {
        status: 'error',
        message: readApiError(error, 'تعذر تحميل الإعدادات. حاول مرة أخرى.'),
      };
    }
  }

  async submit(): Promise<void> {
    if (this.isSubmitting || this.pageState.status !== 'ready') {
      return;
    }

    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    this.successMessage = '';
    this.errorMessage = '';
    this.isSubmitting = true;

    try {
      const payload = this.buildUpdateRequest();
      const updated = await firstValueFrom(this.adminSettingsService.updateSettings(payload));
      this.patchForm(updated);
      this.pageState = { status: 'ready', receiptRetentionMonths: updated.receiptRetentionMonths };
      this.apiCache.invalidate(settingsPublicRequest(environment.apiBaseUrl).url);
      this.successMessage = 'تم حفظ الإعدادات بنجاح.';
    } catch (error) {
      if (this.applyServerValidationErrors(error)) {
        return;
      }

      this.errorMessage = readApiError(error, 'تعذر حفظ الإعدادات. راجع البيانات وحاول مرة أخرى.');
    } finally {
      this.isSubmitting = false;
    }
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

      control.setErrors({ ...control.errors, server: messages[0] });
      control.markAsTouched();
    }

    if (error instanceof HttpErrorResponse) {
      this.errorMessage = readApiError(error, 'تحقق من الحقول المظللة.');
    }

    return true;
  }
}
