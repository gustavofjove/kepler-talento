import { readCandidateOrigin } from '../../src/app/features/candidates/candidate-origin';

describe('readCandidateOrigin (KTL-44)', () => {
  const origin = { kind: 'position', positionId: 'pos-1', positionTitle: 'Analista' };

  it('accepts a position origin', () => {
    expect(readCandidateOrigin({ candidateOrigin: origin })).toEqual(origin);
  });

  it('drops fields it does not know', () => {
    expect(readCandidateOrigin({ candidateOrigin: { ...origin, extra: 'x' } })).toEqual(origin);
  });

  it.each([
    ['no state', null],
    ['undefined', undefined],
    ['a string', 'pos-1'],
    ['another state', { from: 'search' }],
    ['a non-object origin', { candidateOrigin: 'pos-1' }],
    ['a null origin', { candidateOrigin: null }],
    ['a wrong kind', { candidateOrigin: { ...origin, kind: 'candidate' } }],
    ['a missing id', { candidateOrigin: { ...origin, positionId: undefined } }],
    ['an empty id', { candidateOrigin: { ...origin, positionId: '' } }],
    ['a non-string id', { candidateOrigin: { ...origin, positionId: 7 } }],
    ['a missing title', { candidateOrigin: { ...origin, positionTitle: undefined } }],
    ['an empty title', { candidateOrigin: { ...origin, positionTitle: '' } }],
  ])('rejects %s', (_label, state) => {
    expect(readCandidateOrigin(state)).toBeUndefined();
  });
});
