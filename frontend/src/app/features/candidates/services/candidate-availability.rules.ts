import { TranslatableError } from '../../../core/i18n/translatable-error';
import { ALL_AVAILABILITY_STATES, CandidateAvailabilityInput } from '../models/candidate.models';

const DAY = /^\d{4}-\d{2}-\d{2}$/;

function isDay(value: string): boolean {
  if (!DAY.test(value)) return false;
  const parsed = new Date(`${value}T00:00:00Z`);
  return !Number.isNaN(parsed.getTime()) && parsed.toISOString().slice(0, 10) === value;
}

/** The latest check date the API accepts: the current UTC day plus one (KTL-36 design D3). */
export function latestCheckDay(now: Date = new Date()): string {
  const tomorrow = new Date(
    Date.UTC(now.getUTCFullYear(), now.getUTCMonth(), now.getUTCDate() + 1),
  );
  return tomorrow.toISOString().slice(0, 10);
}

/**
 * The availability write's rules, as the API states them, so the form can refuse a check
 * before sending it. The API stays the authority: it applies the same rules and answers with
 * the same refusal.
 *
 * - an `unknown` check carries no dates;
 * - a known check needs a check date no later than the UTC day after `now` (an earlier date
 *   than the stored one is allowed: it amends a mistake);
 * - an until date only on `unavailable`, never before the check date.
 */
export function validateAvailability(
  input: CandidateAvailabilityInput,
  now: Date = new Date(),
): void {
  if (!ALL_AVAILABILITY_STATES.includes(input.state)) {
    throw new TranslatableError('candidate.availability.error.state');
  }
  const checkedOn = input.checkedOn.trim();
  const until = input.until.trim();
  if (input.state === 'unknown') {
    if (checkedOn) throw new TranslatableError('candidate.availability.error.checkedOn');
    if (until) throw new TranslatableError('candidate.availability.error.until');
    return;
  }
  if (!checkedOn || !isDay(checkedOn)) {
    throw new TranslatableError('candidate.availability.error.checkedOn');
  }
  if (checkedOn > latestCheckDay(now)) {
    throw new TranslatableError('candidate.availability.error.checkedOnFuture');
  }
  if (until && (input.state !== 'unavailable' || !isDay(until) || until < checkedOn)) {
    throw new TranslatableError('candidate.availability.error.until');
  }
}
