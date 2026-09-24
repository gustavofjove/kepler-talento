import type { CandidateStatus } from '../../candidates/models/candidate.models';
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

export function selectedStatuses(
  values: CandidateStatus[],
  options: CandidateStatus[],
): CandidateStatus[] {
  if (!values.length) return [...options];
  return options.filter((option) => values.includes(option));
}

export function toggleStatus(
  values: CandidateStatus[],
  options: CandidateStatus[],
  status: CandidateStatus,
  checked: boolean,
): CandidateStatus[] {
  const selected = selectedStatuses(values, options);
  const next = options.filter((option) =>
    option === status ? checked : selected.includes(option),
  );
  return next.length ? next : selected;
}
