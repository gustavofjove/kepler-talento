import { useSyncExternalStore } from 'react';
import type { WritableSignal } from './signal';

/**
 * Subscribes a component to a signal and returns its current value.
 *
 * This takes the signal itself rather than a selector on purpose. A selector
 * such as `() => candidateService.list()` allocates a new array on every call,
 * which makes React throw "The result of getSnapshot should be cached" and loop.
 * Derive from the returned value with `useMemo` instead.
 */
export function useSignal<T>(sig: WritableSignal<T>): T {
  return useSyncExternalStore(sig.subscribe, sig, sig);
}
