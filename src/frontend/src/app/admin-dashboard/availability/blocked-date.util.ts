import { AbstractControl, ValidationErrors, ValidatorFn, Validators } from '@angular/forms';
import { BlockedDate } from '@contracts/availability';
import { APP_LOCALE } from '../../core/i18n/app-locale';
import { CONSULTANT_TIME_ZONE_LABEL } from './availability-window.util';

const MAX_REASON_LENGTH = 500;

export function sortBlockedDates(blockedDates: BlockedDate[]): BlockedDate[] {
  return [...blockedDates].sort((left, right) => left.startUtc.localeCompare(right.startUtc));
}

export function formatBlockedRangeSummary(blockedDate: BlockedDate): string {
  const start = formatUtcInstant(blockedDate.startUtc);
  const end = formatUtcInstant(blockedDate.endUtc);
  const reason = blockedDate.reason ? ` — ${blockedDate.reason}` : '';
  return `${start} – ${end}${reason}`;
}

export function formatUtcInstant(isoUtc: string): string {
  return new Intl.DateTimeFormat(APP_LOCALE, {
    timeZone: CONSULTANT_TIME_ZONE_LABEL,
    dateStyle: 'medium',
    timeStyle: 'short',
  }).format(new Date(isoUtc));
}

export function utcIsoToDatetimeLocal(isoUtc: string): string {
  const date = new Date(isoUtc);
  const pad = (value: number) => String(value).padStart(2, '0');

  return `${date.getUTCFullYear()}-${pad(date.getUTCMonth() + 1)}-${pad(date.getUTCDate())}T${pad(date.getUTCHours())}:${pad(date.getUTCMinutes())}`;
}

export function datetimeLocalToUtcIso(localValue: string): string {
  return `${localValue}:00.000Z`;
}

export function defaultBlockedRangeLocal(): { startUtc: string; endUtc: string } {
  const start = new Date();
  start.setUTCDate(start.getUTCDate() + 1);
  start.setUTCHours(0, 0, 0, 0);

  const end = new Date(start);
  end.setUTCDate(end.getUTCDate() + 1);

  return {
    startUtc: utcIsoToDatetimeLocal(start.toISOString()),
    endUtc: utcIsoToDatetimeLocal(end.toISOString()),
  };
}

export function blockedEndAfterStartValidator(): ValidatorFn {
  return (control: AbstractControl): ValidationErrors | null => {
    const startUtc = control.parent?.get('startUtc')?.value as string | undefined;
    const endUtc = control.value as string | undefined;

    if (!startUtc || !endUtc) {
      return null;
    }

    return startUtc >= endUtc ? { blockedRange: true } : null;
  };
}

export function blockedDateFormValidators(): {
  startUtc: ValidatorFn[];
  endUtc: ValidatorFn[];
  reason: ValidatorFn[];
} {
  return {
    startUtc: [Validators.required],
    endUtc: [Validators.required, blockedEndAfterStartValidator()],
    reason: [Validators.maxLength(MAX_REASON_LENGTH)],
  };
}

export function getBlockedDateFieldError(control: AbstractControl | null): string | null {
  if (!control?.errors || !control.touched) {
    return null;
  }

  if (control.errors['server']) {
    return control.errors['server'] as string;
  }

  if (control.errors['required']) {
    return 'هذا الحقل مطلوب.';
  }

  if (control.errors['blockedRange']) {
    return 'وقت النهاية يجب أن يكون بعد وقت البداية.';
  }

  if (control.errors['maxlength']) {
    return `السبب يجب ألا يتجاوز ${MAX_REASON_LENGTH} حرفًا.`;
  }

  return null;
}
