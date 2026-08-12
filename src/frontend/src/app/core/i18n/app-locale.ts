export const APP_LOCALE = 'ar-EG-u-nu-latn';

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
