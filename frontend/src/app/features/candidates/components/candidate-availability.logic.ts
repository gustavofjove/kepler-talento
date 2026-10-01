import type { TFunction } from 'i18next';
import { formatElapsed } from '../../../core/i18n/format';
import type {
  CandidateAvailability,
  CandidateAvailabilityInput,
  CandidateAvailabilityState,
} from '../models/candidate.models';

/** The value's label: «Sin comprobar», «Disponible» or «No disponible». */
export function availabilityLabel(state: CandidateAvailabilityState, t: TFunction): string {
  return t(`candidate.availability.state.${state}`);
}

/**
 * What a table cell shows (KTL-36): the value and how long ago it was checked, or
 * «Sin comprobar». The until date and the checker are not part of the table projection.
 */
export function availabilityCell(
  state: CandidateAvailabilityState,
  checkedOn: string | null,
  t: TFunction,
  now: Date = new Date(),
): string {
  if (state === 'unknown' || !checkedOn) return availabilityLabel('unknown', t);
  return t('candidate.availability.cell', {
    value: availabilityLabel(state, t),
    elapsed: formatElapsed(checkedOn, now),
  });
}

/** An unavailable check whose until date is before `today` (both `YYYY-MM-DD`). */
export function isLapsed(check: CandidateAvailability, today: string): boolean {
  return check.state === 'unavailable' && check.until !== '' && check.until < today;
}

/** «Sigue igual» is offered only for a known value that has not lapsed. */
export function canReconfirm(check: CandidateAvailability, today: string): boolean {
  return check.state !== 'unknown' && !isLapsed(check, today);
}

/** «Sigue igual»: the stored value and until date again, checked today. */
export function reconfirmInput(
  check: CandidateAvailability,
  today: string,
): CandidateAvailabilityInput {
  return { state: check.state, checkedOn: today, until: check.until };
}

/** «Deshacer»: the previous check resubmitted as it was. */
export function undoInput(previous: CandidateAvailability): CandidateAvailabilityInput {
  return previous.state === 'unknown'
    ? { state: 'unknown', checkedOn: '', until: '' }
    : { state: previous.state, checkedOn: previous.checkedOn, until: previous.until };
}

/** The inline form's fields. */
export interface AvailabilityForm {
  state: CandidateAvailabilityState;
  checkedOn: string;
  until: string;
}

/** The form opened on the stored check, with today's date as the check date. */
export function initialForm(check: CandidateAvailability, today: string): AvailabilityForm {
  return {
    state: check.state,
    checkedOn: today,
    until: check.state === 'unavailable' ? check.until : '',
  };
}

/**
 * The request a submitted form becomes. Fields the chosen value hides are dropped rather than
 * sent: «Sin comprobar» carries no dates and only «No disponible» carries an until date.
 */
export function toRequest(form: AvailabilityForm): CandidateAvailabilityInput {
  if (form.state === 'unknown') return { state: 'unknown', checkedOn: '', until: '' };
  return {
    state: form.state,
    checkedOn: form.checkedOn,
    until: form.state === 'unavailable' ? form.until : '',
  };
}
