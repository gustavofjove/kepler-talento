import { useEffect, useId, useRef, type ReactNode } from 'react';
import '../../../shared/components/modal.css';

interface PickerDialogProps {
  title: string;
  searchLabel: string;
  closeLabel: string;
  query: string;
  onQueryChange: (query: string) => void;
  onClose: () => void;
  testId: string;
  /** Guidance under the search box, e.g. that requirements are not enforced. */
  hint?: string;
  children: ReactNode;
}

const FOCUSABLE = 'button:not([disabled]), input:not([disabled]), select:not([disabled]), a[href]';

/**
 * The modal shell of the KTL-30 pickers: a labelled search box over a list of choices. Focus
 * moves to the search box on open, Tab stays inside the dialog, Escape closes it, and focus
 * returns to whatever opened it.
 */
export function PickerDialog({
  title,
  searchLabel,
  closeLabel,
  query,
  onQueryChange,
  onClose,
  testId,
  hint,
  children,
}: PickerDialogProps) {
  const titleId = useId();
  const searchId = useId();
  const dialog = useRef<HTMLElement>(null);
  const search = useRef<HTMLInputElement>(null);

  useEffect(() => {
    const opener = document.activeElement instanceof HTMLElement ? document.activeElement : null;
    search.current?.focus();
    return () => opener?.focus();
  }, []);

  useEffect(() => {
    const onKeyDown = (event: KeyboardEvent): void => {
      if (event.key === 'Escape') {
        event.preventDefault();
        onClose();
        return;
      }
      if (event.key !== 'Tab' || !dialog.current) return;
      const focusable = [...dialog.current.querySelectorAll<HTMLElement>(FOCUSABLE)];
      if (focusable.length === 0) return;
      const first = focusable[0];
      const last = focusable[focusable.length - 1];
      if (event.shiftKey && document.activeElement === first) {
        event.preventDefault();
        last.focus();
      } else if (!event.shiftKey && document.activeElement === last) {
        event.preventDefault();
        first.focus();
      }
    };
    document.addEventListener('keydown', onKeyDown);
    return () => document.removeEventListener('keydown', onKeyDown);
  }, [onClose]);

  return (
    <div className="overlay" data-testid={`${testId}-overlay`}>
      <section
        ref={dialog}
        className="modal picker-dialog"
        role="dialog"
        aria-modal="true"
        aria-labelledby={titleId}
        data-testid={testId}
      >
        <h2 id={titleId}>{title}</h2>
        <div className="field">
          <label htmlFor={searchId}>{searchLabel}</label>
          <input
            ref={search}
            id={searchId}
            name="pickerSearch"
            type="search"
            autoComplete="off"
            data-testid={`${testId}-search`}
            value={query}
            onChange={(event) => onQueryChange(event.target.value)}
          />
        </div>
        {hint ? <p className="muted">{hint}</p> : null}
        {children}
        <div className="modal-actions">
          <button
            className="button ghost"
            type="button"
            data-testid={`${testId}-close`}
            onClick={onClose}
          >
            {closeLabel}
          </button>
        </div>
      </section>
    </div>
  );
}
