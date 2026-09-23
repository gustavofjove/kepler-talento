import { useServices } from '../../core/di/services-context';
import { useSignal } from '../../core/state/use-signal';
import type { CandidateNotesService } from './services/candidate-notes.service';

export function useCandidateNotes(): CandidateNotesService {
  const { candidateNotesService } = useServices();
  useSignal(candidateNotesService.state);
  return candidateNotesService;
}
