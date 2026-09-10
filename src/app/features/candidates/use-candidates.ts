import { useEffect } from 'react';
import { useServices } from '../../core/di/services-context';
import { useSignal } from '../../core/state/use-signal';
import type { CandidateService } from './services/candidate.service';

/**
 * Returns the candidate service with its signal already subscribed, and triggers the
 * one-time load of the candidate list.
 *
 * Same rationale as `useCatalogs`: the read (`find`, `list`) and the subscription are
 * separate calls, so a component that reaches for `useServices().candidateService`
 * directly during render compiles, renders correctly once, and then silently stops
 * updating. Bundling them here makes that mistake impossible to express.
 */
export function useCandidates(): CandidateService {
  const { candidateService } = useServices();
  useSignal(candidateService.state);
  useEffect(() => {
    void candidateService.ensureLoaded();
  }, [candidateService]);
  return candidateService;
}

/**
 * As `useCandidates`, and additionally loads one complete aggregate — the collections
 * the list endpoint omits — for the screens that show a single candidate.
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
