import {
  cvChoices,
  selectedAvailability,
  toggleCv,
  toggleAvailability,
} from '../../src/app/features/search/components/search-basic-filters.logic';
import type { CandidateAvailabilityState } from '../../src/app/features/candidates/models/candidate.models';

describe('basic search filter selection', () => {
  const states: CandidateAvailabilityState[] = ['unknown', 'available', 'unavailable'];

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

  it('shows empty availability arrays as unrestricted and keeps option order', () => {
    expect(selectedAvailability([], states)).toEqual(states);
    expect(selectedAvailability(['unavailable', 'unknown'], states)).toEqual([
      'unknown',
      'unavailable',
    ]);
    expect(toggleAvailability([], states, 'available', false)).toEqual(['unknown', 'unavailable']);
    expect(toggleAvailability(['unavailable'], states, 'unavailable', false)).toEqual([
      'unavailable',
    ]);
    expect(toggleAvailability(['unavailable'], states, 'unknown', true)).toEqual([
      'unknown',
      'unavailable',
    ]);
  });
});
