import { AbstractControl, ValidationErrors, ValidatorFn, Validators } from '@angular/forms';
import { AvailabilityWindow, DayOfWeek } from '@contracts/availability';

export const CONSULTANT_TIME_ZONE_LABEL = 'Africa/Cairo';

export const DAY_OF_WEEK_OPTIONS: ReadonlyArray<{ value: DayOfWeek; label: string }> = [
  { value: 0, label: 'الأحد' },
  { value: 1, label: 'الإثنين' },
  { value: 2, label: 'الثلاثاء' },
  { value: 3, label: 'الأربعاء' },
  { value: 4, label: 'الخميس' },
  { value: 5, label: 'الجمعة' },
  { value: 6, label: 'السبت' },
];

export function sortWindows(windows: AvailabilityWindow[]): AvailabilityWindow[] {
  return [...windows].sort((left, right) => {
    if (left.dayOfWeek !== right.dayOfWeek) {
      return left.dayOfWeek - right.dayOfWeek;
    }

    return left.startTime.localeCompare(right.startTime);
  });
}

export function formatDayOfWeek(dayOfWeek: DayOfWeek): string {
  return DAY_OF_WEEK_OPTIONS.find((option) => option.value === dayOfWeek)?.label ?? String(dayOfWeek);
}

export function formatLocalTime(time: string): string {
  return toTimeInputValue(time);
}

export function formatWindowSummary(window: AvailabilityWindow): string {
  return `${formatDayOfWeek(window.dayOfWeek)} ${formatLocalTime(window.startTime)}–${formatLocalTime(window.endTime)}`;
}

export function toTimeInputValue(apiTime: string): string {
  const match = /^(\d{2}:\d{2})/.exec(apiTime);
  return match?.[1] ?? apiTime;
}

export function toApiTimeValue(inputTime: string): string {
  return inputTime.length === 5 ? `${inputTime}:00` : inputTime;
}

export function windowEndAfterStartValidator(): ValidatorFn {
  return (control: AbstractControl): ValidationErrors | null => {
    const startTime = control.parent?.get('startTime')?.value as string | undefined;
    const endTime = control.value as string | undefined;

    if (!startTime || !endTime) {
      return null;
    }

    return startTime >= endTime ? { windowRange: true } : null;
  };
}

export function windowFormValidators(): {
  dayOfWeek: ValidatorFn[];
  startTime: ValidatorFn[];
  endTime: ValidatorFn[];
  isActive: ValidatorFn[];
} {
  return {
    dayOfWeek: [Validators.required],
    startTime: [Validators.required],
    endTime: [Validators.required, windowEndAfterStartValidator()],
    isActive: [],
  };
}

export function getWindowFieldError(control: AbstractControl | null, field: string): string | null {
  if (!control?.errors || !control.touched) {
    return null;
  }

  if (control.errors['server']) {
    return control.errors['server'] as string;
  }

  if (control.errors['required']) {
    return 'هذا الحقل مطلوب.';
  }

  if (control.errors['windowRange'] || field === 'endTime' && control.errors['windowRange']) {
    return 'وقت النهاية يجب أن يكون بعد وقت البداية.';
  }

  return null;
}
