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
 * real day is returned unchanged rather than throwing or showing another date.
 */
export function formatDay(
  value: string,
  options: Intl.DateTimeFormatOptions = { dateStyle: 'medium' },
): string {
  if (!/^\d{4}-\d{2}-\d{2}$/.test(value)) return value;
  const day = new Date(`${value}T00:00:00Z`);
  // Some engines roll an impossible day over instead of rejecting it (2026-02-30 → 2 March), so
  // the parsed day must read back as the stored one.
  if (Number.isNaN(day.getTime()) || day.toISOString().slice(0, 10) !== value) return value;
  return formatDate(day, { ...options, timeZone: 'UTC' });
}

export function formatNumber(value: number, options?: Intl.NumberFormatOptions): string {
  return new Intl.NumberFormat(i18n.language, options).format(value);
}
