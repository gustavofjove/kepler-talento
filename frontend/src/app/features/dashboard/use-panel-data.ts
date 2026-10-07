import { useEffect, useState } from 'react';

/** One panel's data: each panel loads, fails and empties on its own (KTL-40). */
export type PanelState<T> =
  { status: 'loading' } | { status: 'ready'; data: T } | { status: 'failed' };

const LOADING = { status: 'loading' } as const;

/**
 * Runs `load` once for a panel the actor may see, and never for one they may not: with `enabled`
 * false no request is sent at all, which is what keeps a hidden panel from asking the API for
 * data the actor would be refused anyway. The request is aborted when the page unmounts.
 *
 * `load` must be stable (wrap it in `useCallback`), or it would run again on every render.
 */
export function usePanelData<T>(
  load: (signal: AbortSignal) => Promise<T>,
  enabled: boolean,
): PanelState<T> {
  const [state, setState] = useState<PanelState<T>>(LOADING);

  useEffect(() => {
    if (!enabled) return;
    const controller = new AbortController();
    load(controller.signal).then(
      (data) => {
        if (!controller.signal.aborted) setState({ status: 'ready', data });
      },
      () => {
        if (!controller.signal.aborted) setState({ status: 'failed' });
      },
    );
    return () => controller.abort();
  }, [load, enabled]);

  return state;
}

/** Combines several panel states: failed if any failed, ready only when all are. */
export function combinePanels<T extends unknown[]>(
  ...states: { [K in keyof T]: PanelState<T[K]> }
): PanelState<T> {
  if (states.some((state) => state.status === 'failed')) return { status: 'failed' };
  if (states.some((state) => state.status === 'loading')) return LOADING;
  return {
    status: 'ready',
    data: states.map((state) => (state as { data: unknown }).data) as T,
  };
}
