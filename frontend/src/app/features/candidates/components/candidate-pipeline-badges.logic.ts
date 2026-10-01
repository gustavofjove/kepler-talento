import type { CandidatePosition, PositionCandidateStage } from '../../positions/position.models';

export type PipelineBadge = 'inProcess' | 'hired';

const IN_PROCESS_STAGES: readonly PositionCandidateStage[] = ['new', 'shortlisted', 'interview'];

/**
 * The badges beside the candidate's name, derived from their position links and never stored
 * (KTL-36):
 *
 * - «En proceso» while at least one link on an open position is still at new, shortlisted or
 *   interview;
 * - «Contratado» when at least one link, on any position, is at hired.
 *
 * Both can show at once.
 */
export function pipelineBadges(links: readonly CandidatePosition[]): PipelineBadge[] {
  const badges: PipelineBadge[] = [];
  if (
    links.some((link) => link.positionStatus === 'open' && IN_PROCESS_STAGES.includes(link.stage))
  ) {
    badges.push('inProcess');
  }
  if (links.some((link) => link.stage === 'hired')) {
    badges.push('hired');
  }
  return badges;
}
