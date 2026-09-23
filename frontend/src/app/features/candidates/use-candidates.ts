import { useEffect } from 'react';
import { useServices } from '../../core/di/services-context';
import { useSignal } from '../../core/state/use-signal';
import type { CandidateService } from './services/candidate.service';

/**
 * Returns the candidate service with its signal already subscribed.
 *
 * Same rationale as `useCatalogs`: the read (`find`, `aggregateStatus`) and the subscription
 * are separate calls, so a component that reaches for `useServices().candidateService`
 * directly during render compiles, renders correctly once, and then silently stops
 * updating. Bundling them here makes that mistake impossible to express.
 *
 * It triggers no load: since KTL-18 there is no whole-table list to load. The list screen
 * asks for pages; single-candidate screens use `useCandidate(id)`.
 */
export function useCandidates(): CandidateService {
  const { candidateService } = useServices();
  useSignal(candidateService.state);
  return candidateService;
}

/**
 * As `useCandidates`, and additionally loads one complete aggregate for the screens that
 * show a single candidate.
 */
export function useCandidate(id: string): CandidateService {
  const candidateService = useCandidates();
  useEffect(() => {
    if (id) {
      void candidateService.ensureAggregate(id);
    }
  }, [candidateService, id]);
  return candidateService;
}
