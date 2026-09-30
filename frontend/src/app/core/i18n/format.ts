import { i18n } from './i18n';

// Use these instead of a hardcoded locale, so formatting follows the active language.
export function formatDate(
  value: Date | string | number,
  options?: Intl.DateTimeFormatOptions,
): string {
  return new Intl.DateTimeFormat(i18n.language, options).format(new Date(value));
}

/**
 * A calendar day stored as `YYYY-MM-DD` (no time, no zone), e.g. «30 sept 2026». It is read and
 * formatted in UTC so no viewer's time zone moves it to the day before. A value that is not a
 * valid day is returned unchanged rather than throwing.
 */
export function formatDay(
  value: string,
  options: Intl.DateTimeFormatOptions = { dateStyle: 'medium' },
): string {
  const day = new Date(`${value}T00:00:00Z`);
  if (!/^\d{4}-\d{2}-\d{2}$/.test(value) || Number.isNaN(day.getTime())) return value;
  return formatDate(day, { ...options, timeZone: 'UTC' });
}

export function formatNumber(value: number, options?: Intl.NumberFormatOptions): string {
  return new Intl.NumberFormat(i18n.language, options).format(value);
}
