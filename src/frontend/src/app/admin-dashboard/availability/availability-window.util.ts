import { AbstractControl, ValidationErrors, ValidatorFn, Validators } from '@angular/forms';
import {
  AvailabilityWindow,
  DAY_OF_WEEK_NAMES,
  DayOfWeek,
  DayOfWeekName,
} from '@contracts/availability';

export const CONSULTANT_TIME_ZONE_LABEL = 'Africa/Cairo';

export const DAY_OF_WEEK_OPTIONS: ReadonlyArray<{ value: DayOfWeekName; label: string }> = [
  { value: 'Sunday', label: 'الأحد' },
  { value: 'Monday', label: 'الإثنين' },
  { value: 'Tuesday', label: 'الثلاثاء' },
  { value: 'Wednesday', label: 'الأربعاء' },
  { value: 'Thursday', label: 'الخميس' },
  { value: 'Friday', label: 'الجمعة' },
  { value: 'Saturday', label: 'السبت' },
];

const DAY_NAME_BY_INDEX = DAY_OF_WEEK_NAMES;

export function parseDayOfWeek(
  value: DayOfWeek | string | number | null | undefined,
): DayOfWeekName {
  if (typeof value === 'number' && Number.isInteger(value) && value >= 0 && value <= 6) {
    return DAY_NAME_BY_INDEX[value] ?? 'Monday';
  }

  if (typeof value === 'string') {
    const trimmed = value.trim();
    if (/^[0-6]$/.test(trimmed)) {
      return DAY_NAME_BY_INDEX[Number(trimmed)] ?? 'Monday';
    }

    const match = DAY_OF_WEEK_OPTIONS.find(
      (option) => option.value.toLowerCase() === trimmed.toLowerCase(),
    );
    if (match) {
      return match.value;
    }
  }

  return 'Monday';
}

export function dayOfWeekIndex(value: DayOfWeek | string | number | null | undefined): number {
  return DAY_NAME_BY_INDEX.indexOf(parseDayOfWeek(value));
}

export function sortWindows(windows: AvailabilityWindow[]): AvailabilityWindow[] {
  return [...windows].sort((left, right) => {
    const dayDelta = dayOfWeekIndex(left.dayOfWeek) - dayOfWeekIndex(right.dayOfWeek);
    if (dayDelta !== 0) {
      return dayDelta;
    }

    return left.startTime.localeCompare(right.startTime);
  });
}

export function formatDayOfWeek(dayOfWeek: DayOfWeek | string | number): string {
  const name = parseDayOfWeek(dayOfWeek);
  return DAY_OF_WEEK_OPTIONS.find((option) => option.value === name)?.label ?? name;
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
    return 'تحقق من صحة القيمة المدخلة في هذا الحقل.';
  }

  if (control.errors['required']) {
    return 'هذا الحقل مطلوب.';
  }

  if (control.errors['windowRange'] || (field === 'endTime' && control.errors['windowRange'])) {
    return 'وقت النهاية يجب أن يكون بعد وقت البداية.';
  }

  return null;
}
