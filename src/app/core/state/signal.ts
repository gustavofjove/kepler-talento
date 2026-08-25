/**
 * Minimal drop-in replacement for the subset of Angular's `signal()` this app
 * actually uses: call-as-getter, `set`, and `update`. Adding `subscribe` is what
 * lets React components observe the value through `useSyncExternalStore`.
 *
 * Deliberately not a general reactive system - there is no `computed` or
 * `effect` anywhere in this codebase, and derived values belong in `useMemo`.
 */
export interface WritableSignal<T> {
  (): T;
  set(value: T): void;
  update(updater: (current: T) => T): void;
  subscribe(listener: () => void): () => void;
}

export function signal<T>(initial: T): WritableSignal<T> {
  let value = initial;
  const listeners = new Set<() => void>();

  const read = (() => value) as WritableSignal<T>;

  read.set = (next: T): void => {
    // Skipping no-op writes keeps useSyncExternalStore from re-rendering when a
    // service persists an unchanged value.
    if (Object.is(next, value)) {
      return;
    }
    value = next;
    for (const listener of [...listeners]) {
      listener();
    }
  };

  read.update = (updater: (current: T) => T): void => {
    read.set(updater(value));
  };

  read.subscribe = (listener: () => void): (() => void) => {
    listeners.add(listener);
    return () => {
      listeners.delete(listener);
    };
  };

  return read;
}
