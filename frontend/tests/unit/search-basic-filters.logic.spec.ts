import {
  cvChoices,
  selectedStatuses,
  toggleCv,
  toggleStatus,
} from '../../src/app/features/search/components/search-basic-filters.logic';
import type { CandidateStatus } from '../../src/app/features/candidates/models/candidate.models';

describe('basic search filter selection', () => {
  const statuses: CandidateStatus[] = ['new', 'available', 'hired'];

  it('maps CV choices to the existing three values and refuses an empty selection', () => {
    expect(cvChoices('')).toEqual({ yes: true, no: true });
    expect(cvChoices('yes')).toEqual({ yes: true, no: false });
    expect(cvChoices('no')).toEqual({ yes: false, no: true });
    expect(toggleCv('', 'no', false)).toBe('yes');
    expect(toggleCv('yes', 'no', true)).toBe('');
    expect(toggleCv('', 'yes', false)).toBe('no');
    expect(toggleCv('no', 'yes', true)).toBe('');
    expect(toggleCv('yes', 'yes', false)).toBe('yes');
  });

  it('shows legacy empty status arrays as unrestricted and keeps option order', () => {
    expect(selectedStatuses([], statuses)).toEqual(statuses);
    expect(selectedStatuses(['hired', 'new'], statuses)).toEqual(['new', 'hired']);
    expect(toggleStatus([], statuses, 'available', false)).toEqual(['new', 'hired']);
    expect(toggleStatus(['hired'], statuses, 'hired', false)).toEqual(['hired']);
    expect(toggleStatus(['hired'], statuses, 'new', true)).toEqual(['new', 'hired']);
  });
});
