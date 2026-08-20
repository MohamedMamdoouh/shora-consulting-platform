import { AbstractControl, ValidationErrors, ValidatorFn, Validators } from '@angular/forms';
import {
  AvailabilityWindow,
  DAY_OF_WEEK_NAMES,
  DayOfWeekName,
} from '@contracts/availability';

import { APP_DISPLAY_TIME_ZONE } from '../../core/i18n/app-locale';
import {
  dayOfWeekIndex,
  formatDayOfWeek,
  parseDayOfWeek,
} from '../../core/i18n/day-of-week.util';

export { APP_DISPLAY_TIME_ZONE as CONSULTANT_TIME_ZONE_LABEL };
export { dayOfWeekIndex, formatDayOfWeek, parseDayOfWeek };

export const DAY_OF_WEEK_OPTIONS: ReadonlyArray<{ value: DayOfWeekName; label: string }> =
  DAY_OF_WEEK_NAMES.map((value, index) => ({
    value,
    label: formatDayOfWeek(index),
  }));

export function sortWindows(windows: AvailabilityWindow[]): AvailabilityWindow[] {
  return [...windows].sort((left, right) => {
    const dayDelta = dayOfWeekIndex(left.dayOfWeek) - dayOfWeekIndex(right.dayOfWeek);
    if (dayDelta !== 0) {
      return dayDelta;
    }

    return left.startTime.localeCompare(right.startTime);
  });
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
