import type { CandidateAvailabilityState } from '../../candidates/models/candidate.models';
import type { SearchFilters } from '../models/search.models';

type CvChoice = 'yes' | 'no';

export function cvChoices(hasCv: SearchFilters['hasCv']): Record<CvChoice, boolean> {
  return { yes: hasCv !== 'no', no: hasCv !== 'yes' };
}

export function toggleCv(
  hasCv: SearchFilters['hasCv'],
  choice: CvChoice,
  checked: boolean,
): SearchFilters['hasCv'] {
  const next = { ...cvChoices(hasCv), [choice]: checked };
  if (!next.yes && !next.no) return hasCv;
  return next.yes && next.no ? '' : next.yes ? 'yes' : 'no';
}

/** The availability values shown as selected: none stored means every one (unrestricted). */
export function selectedAvailability(
  values: CandidateAvailabilityState[],
  options: readonly CandidateAvailabilityState[],
): CandidateAvailabilityState[] {
  if (!values.length) return [...options];
  return options.filter((option) => values.includes(option));
}

/** Checks or unchecks one value, keeping at least one selected. */
export function toggleAvailability(
  values: CandidateAvailabilityState[],
  options: readonly CandidateAvailabilityState[],
  state: CandidateAvailabilityState,
  checked: boolean,
): CandidateAvailabilityState[] {
  const selected = selectedAvailability(values, options);
  const next = options.filter((option) => (option === state ? checked : selected.includes(option)));
  return next.length ? next : selected;
}
