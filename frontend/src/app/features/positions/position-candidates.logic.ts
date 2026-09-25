import { AppError } from '../../shared/models/error.models';
import {
  POSITION_CANDIDATE_STAGES,
  type CandidatePosition,
  type PositionCandidate,
} from './position.models';

const stageOrder = (stage: PositionCandidate['stage']): number =>
  POSITION_CANDIDATE_STAGES.indexOf(stage);

/**
 * The API's order for a position's links: stage, then newest first, then id. Applied again on
 * the client after a local insert or stage change, so the list never has to be refetched.
 */
export function sortPositionCandidates(links: PositionCandidate[]): PositionCandidate[] {
  return [...links].sort(
    (a, b) =>
      stageOrder(a.stage) - stageOrder(b.stage) ||
      b.addedAtUtc.localeCompare(a.addedAtUtc) ||
      a.candidateId.localeCompare(b.candidateId),
  );
}

/** The API's order for a candidate's links: open positions first, then newest first. */
export function sortCandidatePositions(links: CandidatePosition[]): CandidatePosition[] {
  const rank = (link: CandidatePosition): number => (link.positionStatus === 'open' ? 0 : 1);
  return [...links].sort(
    (a, b) =>
      rank(a) - rank(b) ||
      b.addedAtUtc.localeCompare(a.addedAtUtc) ||
      a.positionId.localeCompare(b.positionId),
  );
}

/** Replaces the link with the same candidate, or adds it, and keeps the API order. */
export function upsertPositionCandidate(
  links: PositionCandidate[],
  link: PositionCandidate,
): PositionCandidate[] {
  return sortPositionCandidates([
    ...links.filter((current) => current.candidateId !== link.candidateId),
    link,
  ]);
}

/** Replaces the link with the same position, or adds it, and keeps the API order. */
export function upsertCandidatePosition(
  links: CandidatePosition[],
  link: CandidatePosition,
): CandidatePosition[] {
  return sortCandidatePositions([
    ...links.filter((current) => current.positionId !== link.positionId),
    link,
  ]);
}

/** Whether a failed link write means the list is stale and should be reloaded. */
export function isStaleLinkError(error: unknown): boolean {
  return error instanceof AppError && (error.code === 'CONFLICT' || error.code === 'NOT_FOUND');
}
