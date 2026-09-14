import { useEffect, useId, useRef, type ReactNode } from 'react';
import { useTranslation } from 'react-i18next';
import type { SearchFilters } from '../models/search.models';
import { SearchCriteriaSummary } from './search-criteria-summary';
import '../../../shared/components/modal.css';
import './search-filters.css';

interface SearchCriteriaDialogProps {
  title: string;
  filters: SearchFilters;
  onClose: () => void;
  /** Host-specific facts shown above the criteria, e.g. a preset's timestamps. */
  details?: ReactNode;
  /** Host-specific actions shown beside Close, e.g. editing or applying. */
  actions?: ReactNode;
}

const FOCUSABLE =
  'a[href], button:not([disabled]), input, select, textarea, [tabindex]:not([tabindex="-1"])';

/**
 * A filter set shown read-only in a compact modal. The one way a section shows "what does this
 * search look for" without navigating away: presets today, and the search page and positions
 * after them. The content is always the shared summary; only the details and actions vary.
 */
export function SearchCriteriaDialog({
  title,
  filters,
  onClose,
  details,
  actions,
}: SearchCriteriaDialogProps) {
  const { t } = useTranslation();
  const titleId = useId();
  const dialogRef = useRef<HTMLElement>(null);
  const closeRef = useRef<HTMLButtonElement>(null);
  // Held in a ref so a host passing a new closure on every render does not re-run the effect,
  // which would steal focus back to the close button each time.
  const onCloseRef = useRef(onClose);
  useEffect(() => {
    onCloseRef.current = onClose;
  });

  useEffect(() => {
    const opener = document.activeElement instanceof HTMLElement ? document.activeElement : null;
    closeRef.current?.focus();

    const onKeyDown = (event: KeyboardEvent): void => {
      if (event.key === 'Escape') {
        onCloseRef.current();
        return;
      }
      if (event.key !== 'Tab' || !dialogRef.current) {
        return;
      }
      // Keeps keyboard focus inside the modal, as aria-modal promises.
      const focusable = Array.from(dialogRef.current.querySelectorAll<HTMLElement>(FOCUSABLE));
      if (!focusable.length) {
        return;
      }
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
    return () => {
      document.removeEventListener('keydown', onKeyDown);
      // Back to whatever opened it, typically the row's view button.
      opener?.focus();
    };
  }, []);

  return (
    <div
      className="overlay"
      data-testid="criteria-dialog-overlay"
      onClick={() => onCloseRef.current()}
    >
      <section
        ref={dialogRef}
        className="dialog criteria-dialog"
        role="dialog"
        aria-modal="true"
        aria-labelledby={titleId}
        data-testid="criteria-dialog"
        onClick={(event) => event.stopPropagation()}
      >
        <h2 id={titleId}>{title}</h2>
        {details}
        <SearchCriteriaSummary filters={filters} />
        <div className="actions">
          {actions}
          <button
            ref={closeRef}
            className="button ghost"
            type="button"
            data-testid="criteria-dialog-close"
            onClick={() => onCloseRef.current()}
          >
            {t('search.criteria.dialog.close')}
          </button>
        </div>
      </section>
    </div>
  );
}
