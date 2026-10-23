import { createContext, useContext, useMemo } from 'react';

/**
 * Where a candidate page was opened from, when that changes its breadcrumb (KTL-44). It travels as
 * router location state, never in the URL, and carries no candidate data.
 */
export interface CandidateOrigin {
  kind: 'position';
  positionId: string;
  /** The title shown on the position page at the time of the navigation. */
  positionTitle: string;
}

/** The location state a candidate link carries. */
export interface CandidateLinkState {
  candidateOrigin: CandidateOrigin;
}

/**
 * Set by the position detail page around its content. Every candidate link inside reads it, so the
 * shared tables and the CV panel stay origin-agnostic elsewhere (KTL-44 design D2).
 */
export const CandidateOriginContext = createContext<CandidateOrigin | undefined>(undefined);

/** The state for a candidate link in the current context, or `undefined` outside a position page. */
export function useCandidateLinkState(): CandidateLinkState | undefined {
  const origin = useContext(CandidateOriginContext);
  return useMemo(() => (origin ? { candidateOrigin: origin } : undefined), [origin]);
}

const nonEmpty = (value: unknown): value is string => typeof value === 'string' && value !== '';

/**
 * The origin in a location state, or `undefined` for anything else. The state is whatever the
 * browser's history entry holds, so every field is checked rather than trusted.
 */
export function readCandidateOrigin(state: unknown): CandidateOrigin | undefined {
  if (typeof state !== 'object' || state === null) return undefined;
  const origin: unknown = (state as Partial<CandidateLinkState>).candidateOrigin;
  if (typeof origin !== 'object' || origin === null) return undefined;
  const { kind, positionId, positionTitle } = origin as Partial<CandidateOrigin>;
  if (kind !== 'position' || !nonEmpty(positionId) || !nonEmpty(positionTitle)) return undefined;
  return { kind, positionId, positionTitle };
}
