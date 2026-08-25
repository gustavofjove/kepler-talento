import { useServices } from '../../core/di/services-context';
import { useSignal } from '../../core/state/use-signal';

export function useReferenceCandidate() {
  const { referenceCandidateService } = useServices();
  return {
    candidate: useSignal(referenceCandidateService.candidate),
    loading: useSignal(referenceCandidateService.loading),
    service: referenceCandidateService,
  };
}
