import { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import { useServices } from '../../core/di/services-context';
import type { PositionCandidate, PositionCandidateStage } from './position.models';
import { isStaleLinkError, upsertPositionCandidate } from './position-candidates.logic';

export type LinkListStatus = 'idle' | 'loading' | 'ready' | 'error';

export interface PositionCandidatesState {
  links: PositionCandidate[];
  status: LinkListStatus;
  /** Candidate ids already on the position: what the matches consult for «Añadido». */
  linkedIds: ReadonlySet<string>;
  reload: () => Promise<void>;
  add: (candidateId: string) => Promise<PositionCandidate>;
  changeStage: (link: PositionCandidate, stage: PositionCandidateStage) => Promise<void>;
  remove: (candidateId: string) => Promise<void>;
}

/**
 * The complete link list of one position (KTL-30 design D5). It loads once and is then kept
 * current from each write's server response. A write refused as stale reloads the list before
 * rethrowing, so the caller only has to report the error.
 */
export function usePositionCandidates(
  positionId: string | undefined,
  enabled: boolean,
): PositionCandidatesState {
  const { positionService } = useServices();
  const [links, setLinks] = useState<PositionCandidate[]>([]);
  const [status, setStatus] = useState<LinkListStatus>('idle');
  // Only the latest load may land: a slow earlier response never overwrites a newer one.
  const generation = useRef(0);

  const reload = useCallback(async (): Promise<void> => {
    if (!positionId || !enabled) return;
    const current = ++generation.current;
    setStatus('loading');
    try {
      const loaded = await positionService.listCandidates(positionId);
      if (current !== generation.current) return;
      setLinks(loaded);
      setStatus('ready');
    } catch (error) {
      if (current !== generation.current) return;
      setStatus('error');
      throw error;
    }
  }, [enabled, positionId, positionService]);

  useEffect(() => {
    void reload().catch(() => undefined);
  }, [reload]);

  const staleGuard = useCallback(
    async <T>(write: () => Promise<T>): Promise<T> => {
      try {
        return await write();
      } catch (error) {
        if (isStaleLinkError(error)) void reload().catch(() => undefined);
        throw error;
      }
    },
    [reload],
  );

  const add = useCallback(
    (candidateId: string) =>
      staleGuard(async () => {
        const link = await positionService.addCandidate(positionId!, candidateId);
        setLinks((current) => upsertPositionCandidate(current, link));
        return link;
      }),
    [positionId, positionService, staleGuard],
  );

  const changeStage = useCallback(
    (link: PositionCandidate, stage: PositionCandidateStage) =>
      staleGuard(async () => {
        const next = await positionService.changeStage(
          positionId!,
          link.candidateId,
          stage,
          link.version,
        );
        setLinks((current) => upsertPositionCandidate(current, next));
      }),
    [positionId, positionService, staleGuard],
  );

  const remove = useCallback(
    (candidateId: string) =>
      staleGuard(async () => {
        await positionService.removeCandidate(positionId!, candidateId);
        setLinks((current) => current.filter((link) => link.candidateId !== candidateId));
      }),
    [positionId, positionService, staleGuard],
  );

  const linkedIds = useMemo(() => new Set(links.map((link) => link.candidateId)), [links]);
  return { links, status, linkedIds, reload, add, changeStage, remove };
}
