import {
  isStaleLinkError,
  sortCandidatePositions,
  sortPositionCandidates,
  upsertCandidatePosition,
  upsertPositionCandidate,
} from '../../src/app/features/positions/position-candidates.logic';
import type {
  CandidatePosition,
  PositionCandidate,
} from '../../src/app/features/positions/position.models';
import { AppError } from '../../src/app/shared/models/error.models';

const link = (candidateId: string, stage: PositionCandidate['stage'], addedAtUtc: string) =>
  ({
    candidateId,
    firstName: 'Ana',
    lastName: candidateId,
    email: '',
    phone: '',
    hasPrimaryCv: false,
    candidateIsActive: true,
    stage,
    addedAtUtc,
    updatedAtUtc: addedAtUtc,
    version: 1,
  }) satisfies PositionCandidate;

const position = (
  positionId: string,
  positionStatus: CandidatePosition['positionStatus'],
  addedAtUtc: string,
) =>
  ({
    positionId,
    title: positionId,
    positionStatus,
    stage: 'new',
    addedAtUtc,
    updatedAtUtc: addedAtUtc,
    version: 1,
  }) satisfies CandidatePosition;

describe('position candidate logic', () => {
  it('orders a position’s links by stage, then newest first, then id', () => {
    const sorted = sortPositionCandidates([
      link('c', 'rejected', '2026-09-03T00:00:00Z'),
      link('b', 'new', '2026-09-01T00:00:00Z'),
      link('a', 'interview', '2026-09-05T00:00:00Z'),
      link('d', 'new', '2026-09-02T00:00:00Z'),
    ]);

    expect(sorted.map((item) => item.candidateId)).toEqual(['d', 'b', 'a', 'c']);
  });

  it('orders a candidate’s links with open positions first', () => {
    const sorted = sortCandidatePositions([
      position('closed-new', 'closed', '2026-09-09T00:00:00Z'),
      position('open-old', 'open', '2026-09-01T00:00:00Z'),
      position('open-new', 'open', '2026-09-05T00:00:00Z'),
    ]);

    expect(sorted.map((item) => item.positionId)).toEqual(['open-new', 'open-old', 'closed-new']);
  });

  it('upserts without duplicating and re-sorts', () => {
    const links = [
      link('a', 'new', '2026-09-01T00:00:00Z'),
      link('b', 'new', '2026-09-02T00:00:00Z'),
    ];

    const next = upsertPositionCandidate(links, { ...links[1], stage: 'hired', version: 2 });

    expect(next.map((item) => [item.candidateId, item.stage])).toEqual([
      ['a', 'new'],
      ['b', 'hired'],
    ]);
    expect(
      upsertCandidatePosition([position('p', 'open', '2026-09-01T00:00:00Z')], {
        ...position('p', 'open', '2026-09-01T00:00:00Z'),
        stage: 'interview',
      }),
    ).toHaveLength(1);
  });

  it('treats conflicts and missing links as stale, other failures as not', () => {
    expect(isStaleLinkError(new AppError('CONFLICT'))).toBe(true);
    expect(isStaleLinkError(new AppError('NOT_FOUND'))).toBe(true);
    expect(isStaleLinkError(new AppError('FORBIDDEN'))).toBe(false);
    expect(isStaleLinkError(new Error('x'))).toBe(false);
  });
});
