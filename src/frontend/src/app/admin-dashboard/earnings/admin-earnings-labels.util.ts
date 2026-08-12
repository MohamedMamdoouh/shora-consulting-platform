import { formatCurrency, formatNumber } from '../../core/i18n/app-locale';

export function formatEarningsAmount(amount: number): string {
  return formatCurrency(amount, 'EGP', 2);
}

export function formatEarningsCount(count: number, singular: string, plural: string): string {
  return `${formatNumber(count)} ${count === 1 ? singular : plural}`;
}
