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

const DAY_PATTERN = /^(\d{4})-(\d{2})-(\d{2})$/;

/** The calendar day `date` falls on in the viewer's time zone, as `YYYY-MM-DD`. */
export function localDay(date: Date = new Date()): string {
  const month = String(date.getMonth() + 1).padStart(2, '0');
  const day = String(date.getDate()).padStart(2, '0');
  return `${date.getFullYear()}-${month}-${day}`;
}

/** Year, month and day of a `YYYY-MM-DD` value, or null when it is not a real calendar day. */
function dayParts(value: string): [number, number, number] | null {
  const match = DAY_PATTERN.exec(value);
  if (!match) return null;
  const [year, month, day] = [Number(match[1]), Number(match[2]), Number(match[3])];
  const check = new Date(Date.UTC(year, month - 1, day));
  if (check.getUTCFullYear() !== year || check.getUTCMonth() !== month - 1) return null;
  if (check.getUTCDate() !== day) return null;
  return [year, month, day];
}

/**
 * How long ago a calendar day was, relative to `now`'s day in the viewer's time zone: «hoy»,
 * «ayer», «hace 3 días», «hace 7 meses», «hace 2 años» (KTL-36). Whole calendar days are compared
 * as numbers, never by parsing `YYYY-MM-DD` into a `Date`, which would move it a day in negative
 * UTC offsets. A value that is not a real day yields `''`.
 */
export function formatElapsed(value: string, now: Date = new Date()): string {
  const then = dayParts(value);
  const today = dayParts(localDay(now));
  if (!then || !today) return '';
  const relative = new Intl.RelativeTimeFormat(i18n.language, { numeric: 'auto' });
  const days = Math.round(
    (Date.UTC(today[0], today[1] - 1, today[2]) - Date.UTC(then[0], then[1] - 1, then[2])) /
      86_400_000,
  );
  if (days < 30) return relative.format(-days, 'day');
  const months = (today[0] - then[0]) * 12 + (today[1] - then[1]) - (today[2] < then[2] ? 1 : 0);
  if (months < 12) return relative.format(-Math.max(months, 1), 'month');
  return relative.format(-Math.floor(months / 12), 'year');
}

export function formatNumber(value: number, options?: Intl.NumberFormatOptions): string {
  return new Intl.NumberFormat(i18n.language, options).format(value);
}
