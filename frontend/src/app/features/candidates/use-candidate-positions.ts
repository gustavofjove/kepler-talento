import { useCallback, useEffect, useRef, useState } from 'react';
import { useServices } from '../../core/di/services-context';
import type { CandidatePosition } from '../positions/position.models';

export type CandidatePositionsStatus = 'idle' | 'loading' | 'ready' | 'error';

export interface CandidatePositions {
  links: CandidatePosition[];
  status: CandidatePositionsStatus;
  /** Replaces the list after a change the panel made itself (a stage change, a removal). */
  setLinks: (update: (current: CandidatePosition[]) => CandidatePosition[]) => void;
  reload: () => Promise<void>;
}

/**
 * The candidate's position links, loaded once for the page (KTL-36 design D8). The «Posiciones»
 * panel and the pipeline badges read the same list, so a stage change shows in both without a
 * second request. Nothing is requested while `enabled` is false: an actor without
 * `positions.read` sends no position request at all.
 */
export function useCandidatePositions(candidateId: string, enabled: boolean): CandidatePositions {
  const { positionService } = useServices();
  const [links, setLinksState] = useState<CandidatePosition[]>([]);
  const [status, setStatus] = useState<CandidatePositionsStatus>('idle');
  const generation = useRef(0);

  const reload = useCallback(async (): Promise<void> => {
    if (!enabled) return;
    const current = ++generation.current;
    setStatus('loading');
    try {
      const loaded = await positionService.listForCandidate(candidateId);
      if (current !== generation.current) return;
      setLinksState(loaded);
      setStatus('ready');
    } catch {
      if (current === generation.current) setStatus('error');
    }
  }, [candidateId, enabled, positionService]);

  useEffect(() => {
    if (!enabled) {
      generation.current++;
      setLinksState([]);
      setStatus('idle');
      return;
    }
    void reload();
  }, [enabled, reload]);

  const setLinks = useCallback(
    (update: (current: CandidatePosition[]) => CandidatePosition[]) => setLinksState(update),
    [],
  );

  return { links, status, setLinks, reload };
}
