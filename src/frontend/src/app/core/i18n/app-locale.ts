export const APP_LOCALE = 'ar-EG-u-nu-latn';

export const APP_DISPLAY_TIME_ZONE = 'Africa/Cairo';

const ARABIC_DAY_LABELS = [
  'الأحد',
  'الإثنين',
  'الثلاثاء',
  'الأربعاء',
  'الخميس',
  'الجمعة',
  'السبت',
] as const;

const WEEKDAY_INDEX_BY_SHORT_LABEL: Record<string, number> = {
  Sun: 0,
  Mon: 1,
  Tue: 2,
  Wed: 3,
  Thu: 4,
  Fri: 5,
  Sat: 6,
};

export function formatLocalDayOfWeek(dayIndex: number): string {
  return ARABIC_DAY_LABELS[dayIndex] ?? ARABIC_DAY_LABELS[0];
}

function getLocalDayIndex(date: Date, timeZone?: string): number {
  if (!timeZone) {
    return date.getDay();
  }

  const weekday = new Intl.DateTimeFormat('en-US', {
    timeZone,
    weekday: 'short',
  }).format(date);

  return WEEKDAY_INDEX_BY_SHORT_LABEL[weekday] ?? date.getDay();
}

export function formatZonedDateKey(
  value: Date | string | number,
  timeZone: string = APP_DISPLAY_TIME_ZONE,
): string {
  const date = value instanceof Date ? value : new Date(value);
  const parts = Object.fromEntries(
    new Intl.DateTimeFormat('en-US', {
      timeZone,
      year: 'numeric',
      month: '2-digit',
      day: '2-digit',
    })
      .formatToParts(date)
      .filter((part) => part.type !== 'literal')
      .map((part) => [part.type, part.value]),
  );

  return `${parts['year']}-${parts['month']}-${parts['day']}`;
}

export function formatLocalDateTimeWithDay(
  value: Date | string | number,
  options?: { timeZone?: string },
): string {
  const date = value instanceof Date ? value : new Date(value);
  if (Number.isNaN(date.getTime())) {
    return typeof value === 'string' ? value : '';
  }

  const { timeZone } = options ?? {};
  const dateTimeOptions = timeZone ? { timeZone } : undefined;
  const dayIndex = getLocalDayIndex(date, timeZone);

  const datePart = formatDateTime(date, {
    ...dateTimeOptions,
    day: 'numeric',
    month: 'long',
  });
  const yearPart = formatDateTime(date, {
    ...dateTimeOptions,
    year: 'numeric',
  });
  const timeLabel = formatDateTime(date, {
    ...dateTimeOptions,
    hour: 'numeric',
    minute: '2-digit',
    hour12: true,
  });

  return `${formatLocalDayOfWeek(dayIndex)}، ${datePart}، ${yearPart} · ${timeLabel}`;
}

export function formatLocalDateWithDay(
  value: Date | string | number,
  options?: { timeZone?: string; includeYear?: boolean },
): string {
  const date = value instanceof Date ? value : new Date(value);
  if (Number.isNaN(date.getTime())) {
    return typeof value === 'string' ? value : '';
  }

  const { timeZone, includeYear = true } = options ?? {};
  const dateTimeOptions = timeZone ? { timeZone } : undefined;
  const dayIndex = getLocalDayIndex(date, timeZone);

  const datePart = formatDateTime(date, {
    ...dateTimeOptions,
    day: 'numeric',
    month: 'long',
  });

  if (!includeYear) {
    return `${formatLocalDayOfWeek(dayIndex)}، ${datePart}`;
  }

  const yearPart = formatDateTime(date, {
    ...dateTimeOptions,
    year: 'numeric',
  });

  return `${formatLocalDayOfWeek(dayIndex)}، ${datePart}، ${yearPart}`;
}

export function formatLocalTime(
  value: Date | string | number,
  options?: { timeZone?: string },
): string {
  const date = value instanceof Date ? value : new Date(value);
  if (Number.isNaN(date.getTime())) {
    return typeof value === 'string' ? value : '';
  }

  const { timeZone } = options ?? {};
  const dateTimeOptions = timeZone ? { timeZone } : undefined;

  return formatDateTime(date, {
    ...dateTimeOptions,
    hour: 'numeric',
    minute: '2-digit',
    hour12: true,
  });
}

export function formatDisplayUtcDateTime(value: Date | string | number): string {
  return formatLocalDateTimeWithDay(value, { timeZone: APP_DISPLAY_TIME_ZONE });
}

export function formatNumber(value: number, options?: Intl.NumberFormatOptions): string {
  return new Intl.NumberFormat(APP_LOCALE, options).format(value);
}

const CURRENCY_LABELS: Readonly<Record<string, string>> = {
  EGP: 'جنيه',
};

export function formatCurrencyLabel(currency: string): string {
  return CURRENCY_LABELS[currency.toUpperCase()] ?? currency;
}

export function formatCurrency(
  amount: number,
  currency: string,
  maximumFractionDigits = 0,
): string {
  return `${formatNumber(amount, {
    minimumFractionDigits: 0,
    maximumFractionDigits,
  })} ${formatCurrencyLabel(currency)}`;
}

export function formatDurationMinutes(minutes: number, unitLabel = 'دقيقة'): string {
  return `${formatNumber(minutes)} ${unitLabel}`;
}

export function formatDateTime(
  value: Date | string | number,
  options?: Intl.DateTimeFormatOptions,
): string {
  const date = value instanceof Date ? value : new Date(value);
  return new Intl.DateTimeFormat(APP_LOCALE, options).format(date);
}
