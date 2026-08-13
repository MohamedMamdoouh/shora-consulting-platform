import { Component, computed, inject, OnInit, signal } from '@angular/core';
import { FormBuilder, FormGroup, ReactiveFormsModule } from '@angular/forms';
import { HttpErrorResponse } from '@angular/common/http';
import {
  AvailabilityWindow,
  BlockedDate,
  CreateAvailabilityWindowRequest,
  CreateBlockedDateRequest,
  DayOfWeek,
  UpdateAvailabilityWindowRequest,
} from '@contracts/availability';
import { ErrorCodes } from '@contracts/error-codes';
import { firstValueFrom } from 'rxjs';
import {
  readApiError,
  readApiErrorCode,
  readConflictingBookingIds,
  readValidationErrors,
} from '../../core/api/api-error.util';
import { ApiCacheService } from '../../core/api/api-cache.service';
import { ConfirmDialogService } from '../../core/ui/confirm-dialog.service';
import { APP_COPY } from '../../core/i18n/app-copy.constants';
import { AdminAvailabilityService } from '../../core/admin/admin-availability.service';
import { AdminBlockedDateService } from '../../core/admin/admin-blocked-date.service';
import { environment } from '../../../environments/environment';
import {
  blockedDateFormValidators,
  datetimeLocalToUtcIso,
  defaultBlockedRangeLocal,
  formatBlockedRangeSummary,
  getBlockedDateFieldError,
  sortBlockedDates,
} from './blocked-date.util';
import {
  CONSULTANT_TIME_ZONE_LABEL,
  DAY_OF_WEEK_OPTIONS,
  formatWindowSummary,
  getWindowFieldError,
  sortWindows,
  toApiTimeValue,
  toTimeInputValue,
  windowFormValidators,
} from './availability-window.util';

type PageState =
  | { status: 'loading' }
  | { status: 'error'; message: string }
  | { status: 'ready'; windows: AvailabilityWindow[]; blockedDates: BlockedDate[] };

@Component({
  selector: 'app-admin-availability-page',
  imports: [ReactiveFormsModule],
  templateUrl: './admin-availability-page.component.html',
  styleUrl: './admin-availability-page.component.scss',
})
export class AdminAvailabilityPageComponent implements OnInit {
  private readonly fb = inject(FormBuilder);
  private readonly adminAvailabilityService = inject(AdminAvailabilityService);
  private readonly adminBlockedDateService = inject(AdminBlockedDateService);
  private readonly apiCache = inject(ApiCacheService);
  private readonly confirmDialog = inject(ConfirmDialogService);

  protected readonly copy = APP_COPY;

  readonly pageState = signal<PageState>({ status: 'loading' });
  readonly editingWindowId = signal<string | null>(null);
  readonly successMessage = signal('');
  readonly errorMessage = signal('');
  readonly isSubmitting = signal(false);
  readonly deletingWindowId = signal<string | null>(null);

  readonly blockedSuccessMessage = signal('');
  readonly blockedErrorMessage = signal('');
  readonly conflictingBookingIds = signal<string[]>([]);
  readonly isSubmittingBlocked = signal(false);
  readonly deletingBlockedDateId = signal<string | null>(null);

  readonly isEditing = computed(() => this.editingWindowId() !== null);

  readonly formTitle = computed(() =>
    this.isEditing() ? 'تعديل نافذة التوفر' : 'إضافة نافذة توفر',
  );

  readonly submitLabel = computed(() => {
    if (this.isSubmitting()) {
      return this.isEditing() ? 'جاري الحفظ...' : 'جاري الإضافة...';
    }

    return this.isEditing() ? 'حفظ التعديلات' : 'إضافة النافذة';
  });

  readonly blockedSubmitLabel = computed(() =>
    this.isSubmittingBlocked() ? 'جاري الإضافة...' : 'إضافة الحجب',
  );

  readonly dayOptions = DAY_OF_WEEK_OPTIONS;
  readonly consultantTimeZoneLabel = CONSULTANT_TIME_ZONE_LABEL;
  readonly formatWindowSummary = formatWindowSummary;
  readonly formatBlockedRangeSummary = formatBlockedRangeSummary;
  readonly getFieldError = getWindowFieldError;
  readonly getBlockedFieldError = getBlockedDateFieldError;

  readonly form = this.fb.nonNullable.group({
    dayOfWeek: this.fb.nonNullable.control<DayOfWeek>(1, windowFormValidators().dayOfWeek),
    startTime: this.fb.nonNullable.control('16:00', windowFormValidators().startTime),
    endTime: this.fb.nonNullable.control('21:00', windowFormValidators().endTime),
    isActive: this.fb.nonNullable.control(true, windowFormValidators().isActive),
  });

  readonly blockedForm = this.fb.nonNullable.group({
    startUtc: this.fb.nonNullable.control(
      defaultBlockedRangeLocal().startUtc,
      blockedDateFormValidators().startUtc,
    ),
    endUtc: this.fb.nonNullable.control(
      defaultBlockedRangeLocal().endUtc,
      blockedDateFormValidators().endUtc,
    ),
    reason: this.fb.control<string | null>(null, blockedDateFormValidators().reason),
  });

  ngOnInit(): void {
    this.form.controls.startTime.valueChanges.subscribe(() => {
      this.form.controls.endTime.updateValueAndValidity({ emitEvent: false });
    });

    this.blockedForm.controls.startUtc.valueChanges.subscribe(() => {
      this.blockedForm.controls.endUtc.updateValueAndValidity({ emitEvent: false });
    });

    void this.loadPage();
  }

  async loadPage(): Promise<void> {
    this.pageState.set({ status: 'loading' });
    this.clearWindowMessages();
    this.clearBlockedMessages();

    try {
      const [windows, blockedDates] = await Promise.all([
        firstValueFrom(this.adminAvailabilityService.listWindows()),
        firstValueFrom(this.adminBlockedDateService.listBlockedDates()),
      ]);

      this.pageState.set({
        status: 'ready',
        windows: sortWindows(windows),
        blockedDates: sortBlockedDates(blockedDates),
      });
    } catch (error) {
      this.pageState.set({
        status: 'error',
        message: readApiError(error, 'تعذر تحميل صفحة المواعيد. حاول مرة أخرى.'),
      });
    }
  }

  startCreate(): void {
    this.editingWindowId.set(null);
    this.clearWindowMessages();
    this.form.reset({
      dayOfWeek: 1 as DayOfWeek,
      startTime: '16:00',
      endTime: '21:00',
      isActive: true,
    });
  }

  startEdit(window: AvailabilityWindow): void {
    this.editingWindowId.set(window.id);
    this.clearWindowMessages();
    this.form.reset({
      dayOfWeek: window.dayOfWeek,
      startTime: toTimeInputValue(window.startTime),
      endTime: toTimeInputValue(window.endTime),
      isActive: window.isActive,
    });
  }

  cancelEdit(): void {
    this.startCreate();
  }

  resetBlockedForm(): void {
    this.clearBlockedMessages();
    const defaults = defaultBlockedRangeLocal();
    this.blockedForm.reset({
      startUtc: defaults.startUtc,
      endUtc: defaults.endUtc,
      reason: null,
    });
  }

  async submit(): Promise<void> {
    if (this.isSubmitting() || this.pageState().status !== 'ready') {
      return;
    }

    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    this.clearWindowMessages();
    this.isSubmitting.set(true);

    try {
      const payload = this.buildWindowPayload();
      const editingId = this.editingWindowId();

      if (editingId) {
        await firstValueFrom(this.adminAvailabilityService.updateWindow(editingId, payload));
        this.successMessage.set('تم تحديث نافذة التوفر.');
      } else {
        await firstValueFrom(this.adminAvailabilityService.createWindow(payload));
        this.successMessage.set('تمت إضافة نافذة التوفر.');
      }

      this.invalidateAvailabilityCache();
      this.startCreate();
      await this.refreshWindows();
    } catch (error) {
      if (this.applyWindowServerValidationErrors(error)) {
        return;
      }

      this.errorMessage.set(
        readApiError(error, 'تعذر حفظ نافذة التوفر. راجع البيانات وحاول مرة أخرى.'),
      );
    } finally {
      this.isSubmitting.set(false);
    }
  }

  async submitBlockedDate(): Promise<void> {
    if (this.isSubmittingBlocked() || this.pageState().status !== 'ready') {
      return;
    }

    if (this.blockedForm.invalid) {
      this.blockedForm.markAllAsTouched();
      return;
    }

    this.clearBlockedMessages();
    this.isSubmittingBlocked.set(true);

    try {
      await firstValueFrom(
        this.adminBlockedDateService.createBlockedDate(this.buildBlockedDatePayload()),
      );

      this.invalidateAvailabilityCache();
      this.blockedSuccessMessage.set('تمت إضافة فترة الحجب.');
      this.resetBlockedForm();
      await this.refreshBlockedDates();
    } catch (error) {
      if (this.applyBlockedServerValidationErrors(error)) {
        return;
      }

      const conflictingIds = readConflictingBookingIds(error);
      if (conflictingIds?.length) {
        this.conflictingBookingIds.set(conflictingIds);
        this.blockedErrorMessage.set(
          'تتعارض فترة الحجب مع حجوزات قائمة. قم بإلغاءهذه الحجوزات أولًا ثم حاول مرة أخرى.',
        );
        return;
      }

      if (readApiErrorCode(error) === ErrorCodes.Availability.BlockedRangeConflictsWithBookings) {
        this.blockedErrorMessage.set(
          'تتعارض فترة الحجب مع حجوزات قائمة. قم بإلغاءالحجوزات المتعارضة أولًا ثم حاول مرة أخرى.',
        );
        return;
      }

      this.blockedErrorMessage.set(
        readApiError(error, 'تعذر إضافة فترة الحجب. تحقق من البيانات وحاول مرة أخرى.'),
      );
    } finally {
      this.isSubmittingBlocked.set(false);
    }
  }

  async deleteWindow(window: AvailabilityWindow): Promise<void> {
    if (this.pageState().status !== 'ready' || this.deletingWindowId()) {
      return;
    }

    const confirmed = await this.confirmDialog.confirm({
      title: this.copy.admin.dialog.deleteWindowTitle,
      message: this.copy.admin.dialog.deleteWindowMessage(formatWindowSummary(window)),
      confirmLabel: this.copy.admin.dialog.deleteWindowAction,
      variant: 'danger',
    });
    if (!confirmed) {
      return;
    }

    this.clearWindowMessages();
    this.deletingWindowId.set(window.id);

    try {
      await firstValueFrom(this.adminAvailabilityService.deleteWindow(window.id));
      this.invalidateAvailabilityCache();

      if (this.editingWindowId() === window.id) {
        this.startCreate();
      }

      this.successMessage.set('تم حذف نافذة التوفر.');
      await this.refreshWindows();
    } catch (error) {
      this.errorMessage.set(readApiError(error, 'تعذر حذف نافذة التوفر. حاول مرة أخرى.'));
    } finally {
      this.deletingWindowId.set(null);
    }
  }

  async deleteBlockedDate(blockedDate: BlockedDate): Promise<void> {
    if (this.pageState().status !== 'ready' || this.deletingBlockedDateId()) {
      return;
    }

    const confirmed = await this.confirmDialog.confirm({
      title: this.copy.admin.dialog.removeBlockedDateTitle,
      message: this.copy.admin.dialog.removeBlockedDateMessage(
        formatBlockedRangeSummary(blockedDate),
      ),
      confirmLabel: this.copy.admin.dialog.removeBlockedDateAction,
      variant: 'danger',
    });
    if (!confirmed) {
      return;
    }

    this.clearBlockedMessages();
    this.deletingBlockedDateId.set(blockedDate.id);

    try {
      await firstValueFrom(this.adminBlockedDateService.deleteBlockedDate(blockedDate.id));
      this.invalidateAvailabilityCache();
      this.blockedSuccessMessage.set('تمت إزالة فترة الحجب.');
      await this.refreshBlockedDates();
    } catch (error) {
      this.blockedErrorMessage.set(readApiError(error, 'تعذر إزالة فترة الحجب. حاول مرة أخرى.'));
    } finally {
      this.deletingBlockedDateId.set(null);
    }
  }

  private async refreshWindows(): Promise<void> {
    const current = this.pageState();
    if (current.status !== 'ready') {
      await this.loadPage();
      return;
    }

    try {
      const windows = await firstValueFrom(this.adminAvailabilityService.listWindows());
      this.pageState.set({ ...current, windows: sortWindows(windows) });
    } catch (error) {
      this.pageState.set({
        status: 'error',
        message: readApiError(error, 'تعذر تحميل نوافذ التوفر. حاول مرة أخرى.'),
      });
    }
  }

  private async refreshBlockedDates(): Promise<void> {
    const current = this.pageState();
    if (current.status !== 'ready') {
      await this.loadPage();
      return;
    }

    try {
      const blockedDates = await firstValueFrom(this.adminBlockedDateService.listBlockedDates());
      this.pageState.set({ ...current, blockedDates: sortBlockedDates(blockedDates) });
    } catch (error) {
      this.blockedErrorMessage.set(readApiError(error, 'تعذر تحديث قائمة فترات الحجب.'));
    }
  }

  private buildWindowPayload(): CreateAvailabilityWindowRequest & UpdateAvailabilityWindowRequest {
    const raw = this.form.getRawValue();

    return {
      dayOfWeek: raw.dayOfWeek,
      startTime: toApiTimeValue(raw.startTime),
      endTime: toApiTimeValue(raw.endTime),
      isActive: raw.isActive,
    };
  }

  private buildBlockedDatePayload(): CreateBlockedDateRequest {
    const raw = this.blockedForm.getRawValue();
    const reason = raw.reason?.trim();

    return {
      startUtc: datetimeLocalToUtcIso(raw.startUtc),
      endUtc: datetimeLocalToUtcIso(raw.endUtc),
      reason: reason ? reason : null,
    };
  }

  private invalidateAvailabilityCache(): void {
    this.apiCache.invalidateUrlPrefix(`${environment.apiBaseUrl}/availability`);
  }

  private clearWindowMessages(): void {
    this.successMessage.set('');
    this.errorMessage.set('');
  }

  private clearBlockedMessages(): void {
    this.blockedSuccessMessage.set('');
    this.blockedErrorMessage.set('');
    this.conflictingBookingIds.set([]);
  }

  private applyWindowServerValidationErrors(error: unknown): boolean {
    return this.applyServerValidationErrors(error, this.form, (message) => {
      this.errorMessage.set(message);
    });
  }

  private applyBlockedServerValidationErrors(error: unknown): boolean {
    return this.applyServerValidationErrors(error, this.blockedForm, (message) => {
      this.blockedErrorMessage.set(message);
    });
  }

  private applyServerValidationErrors(
    error: unknown,
    form: FormGroup,
    setMessage: (message: string) => void,
  ): boolean {
    const fieldErrors = readValidationErrors(error);
    if (!fieldErrors) {
      return false;
    }

    for (const [field, messages] of Object.entries(fieldErrors)) {
      const control = form.get(field);
      if (!control || messages.length === 0) {
        continue;
      }

      control.setErrors({ ...control.errors, server: messages[0] });
      control.markAsTouched();
    }

    if (error instanceof HttpErrorResponse) {
      setMessage(readApiError(error, 'تحقق من الحقول المظللة.'));
    }

    return true;
  }
}
