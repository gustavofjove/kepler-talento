import { useEffect, useState } from 'react';

/**
 * The staged copy of what a form panel edits (KTL-29 design D2). Until the user changes
 * something the panel shows the saved value; leaving edit mode drops the draft, so the next
 * «Editar» starts from what is saved. `dirty` compares content, so undoing a change by hand
 * makes the panel clean again.
 */
export function usePanelDraft<T>(
  saved: T,
  editing: boolean,
  onDirtyChange: (dirty: boolean) => void,
): { value: T; set: (next: T) => void; dirty: boolean; reset: () => void } {
  const [draft, setDraft] = useState<{ value: T } | null>(null);

  useEffect(() => {
    if (!editing) setDraft(null);
  }, [editing]);

  const value = draft ? draft.value : saved;
  const dirty = draft !== null && JSON.stringify(draft.value) !== JSON.stringify(saved);

  useEffect(() => onDirtyChange(dirty), [dirty, onDirtyChange]);

  return {
    value,
    set: (next) => setDraft({ value: next }),
    dirty,
    reset: () => setDraft(null),
  };
}
