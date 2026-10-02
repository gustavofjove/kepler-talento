import { localDay } from '../../../core/i18n/format';

export const REVIEW_DUE_SOON_DAYS = 30;

function calendarDay(value: string): number | null {
  if (!/^\d{4}-\d{2}-\d{2}$/.test(value)) return null;
  const day = new Date(`${value}T00:00:00Z`);
  if (Number.isNaN(day.getTime()) || day.toISOString().slice(0, 10) !== value) return null;
  return day.getTime() / 86_400_000;
}

export type ReviewUrgency = 'overdue' | 'dueSoon' | null;

export function reviewUrgency(reviewDueAt: string, now: Date = new Date()): ReviewUrgency {
  const due = calendarDay(reviewDueAt);
  const today = calendarDay(localDay(now));
  if (due === null || today === null) return null;
  const daysUntilDue = due - today;
  if (daysUntilDue < 0) return 'overdue';
  return daysUntilDue <= REVIEW_DUE_SOON_DAYS ? 'dueSoon' : null;
}

export function showReception(receivedAt: string, createdAt: string): boolean {
  if (!receivedAt) return true;
  const created = new Date(createdAt);
  if (Number.isNaN(created.getTime())) return true;
  return receivedAt !== localDay(created);
}
