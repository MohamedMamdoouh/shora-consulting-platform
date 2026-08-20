import { AbstractControl, ValidationErrors, ValidatorFn, Validators } from '@angular/forms';

import { BlockedDate } from '@contracts/availability';

import { formatDisplayUtcDateTime, APP_DISPLAY_TIME_ZONE } from '../../core/i18n/app-locale';

const MAX_REASON_LENGTH = 500;

const DATETIME_LOCAL_PATTERN = /^(\d{4})-(\d{2})-(\d{2})T(\d{2}):(\d{2})$/;

function pad(value: number): string {
  return String(value).padStart(2, '0');
}

function getZonedDateTimeParts(date: Date, timeZone: string): Record<string, string> {
  const formatter = new Intl.DateTimeFormat('en-US', {
    timeZone,

    hour12: false,

    year: 'numeric',

    month: '2-digit',

    day: '2-digit',

    hour: '2-digit',

    minute: '2-digit',

    second: '2-digit',
  });

  return Object.fromEntries(
    formatter

      .formatToParts(date)

      .filter((part) => part.type !== 'literal')

      .map((part) => [part.type, part.value]),
  );
}

function zonedPartsToDatetimeLocal(parts: Record<string, string>): string {
  return `${parts['year']}-${parts['month']}-${parts['day']}T${parts['hour']}:${parts['minute']}`;
}

function getTimeZoneOffsetMs(date: Date, timeZone: string): number {
  const parts = getZonedDateTimeParts(date, timeZone);

  const asUtc = Date.UTC(
    Number(parts['year']),

    Number(parts['month']) - 1,

    Number(parts['day']),

    Number(parts['hour']),

    Number(parts['minute']),

    Number(parts['second']),
  );

  return asUtc - date.getTime();
}

export function sortBlockedDates(blockedDates: BlockedDate[]): BlockedDate[] {
  return [...blockedDates].sort((left, right) => left.startUtc.localeCompare(right.startUtc));
}

export function formatBlockedRangeSummary(blockedDate: BlockedDate): string {
  const start = formatDisplayUtcDateTime(blockedDate.startUtc);

  const end = formatDisplayUtcDateTime(blockedDate.endUtc);

  const reason = blockedDate.reason ? ` — ${blockedDate.reason}` : '';

  return `${start} – ${end}${reason}`;
}

export function utcIsoToDatetimeLocal(isoUtc: string): string {
  const parts = getZonedDateTimeParts(new Date(isoUtc), APP_DISPLAY_TIME_ZONE);

  return zonedPartsToDatetimeLocal(parts);
}

export function datetimeLocalToUtcIso(localValue: string): string {
  const match = DATETIME_LOCAL_PATTERN.exec(localValue);

  if (!match) {
    throw new Error(`Invalid datetime-local value: ${localValue}`);
  }

  const [, year, month, day, hour, minute] = match.map(Number);

  let utcMs = Date.UTC(year, month - 1, day, hour, minute);

  for (let iteration = 0; iteration < 2; iteration++) {
    const offsetMs = getTimeZoneOffsetMs(new Date(utcMs), APP_DISPLAY_TIME_ZONE);

    utcMs = Date.UTC(year, month - 1, day, hour, minute) - offsetMs;
  }

  return new Date(utcMs).toISOString();
}

export function defaultBlockedRangeLocal(): { startUtc: string; endUtc: string } {
  const cairoNow = getZonedDateTimeParts(new Date(), APP_DISPLAY_TIME_ZONE);

  const year = Number(cairoNow['year']);

  const month = Number(cairoNow['month']);

  const day = Number(cairoNow['day']);

  const tomorrow = new Date(Date.UTC(year, month - 1, day + 1));

  const dayAfter = new Date(Date.UTC(year, month - 1, day + 2));

  return {
    startUtc: `${tomorrow.getUTCFullYear()}-${pad(tomorrow.getUTCMonth() + 1)}-${pad(tomorrow.getUTCDate())}T00:00`,

    endUtc: `${dayAfter.getUTCFullYear()}-${pad(dayAfter.getUTCMonth() + 1)}-${pad(dayAfter.getUTCDate())}T00:00`,
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
    return 'تحقق من صحة القيمة المدخلة في هذا الحقل.';
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
