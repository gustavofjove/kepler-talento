import { useServices } from '../../core/di/services-context';
import { useSignal } from '../../core/state/use-signal';
import type { CandidateService } from './services/candidate.service';

/**
 * Returns the candidate service with its signal already subscribed.
 * Same rationale as useCatalogs: keeps the subscription and the read together.
 */
export function useCandidates(): CandidateService {
  const { candidateService } = useServices();
  useSignal(candidateService.candidates);
  return candidateService;
}
