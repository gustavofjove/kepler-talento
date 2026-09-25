import { useCallback, type MouseEvent } from 'react';
import { useNavigate } from 'react-router';
import './row-link.css';

/**
 * Clicks on these keep their own behaviour: a row never hijacks a real control. A cell marked
 * `data-row-link-ignore` (e.g. a selection checkbox cell) opts out as a whole, so a near-miss
 * around its control never navigates.
 */
const INTERACTIVE =
  'a, button, input, select, textarea, label, [role="button"], [data-row-link-ignore]';

/**
 * Whether a click on a row belongs to the row itself: a primary or middle button, outside every
 * link and control, and not the end of a text selection. Rows that act in place (e.g. starting an
 * inline edit) use this directly; rows that open a record use `useRowLink`.
 */
export function isRowClick(event: MouseEvent<HTMLElement>): boolean {
  if (event.defaultPrevented || (event.button !== 0 && event.button !== 1)) return false;
  if ((event.target as Element).closest(INTERACTIVE)) return false;
  return !window.getSelection()?.toString();
}

/**
 * Makes a whole table row open `to`, while every link, button and control inside it keeps working
 * and selecting text never navigates. Ctrl/⌘-click and middle-click open a new tab, as a link would.
 *
 * The row is a mouse convenience only: keep a real link in the row (e.g. the name, styled with
 * `.row-link`) so keyboard and screen-reader users reach the same destination.
 */
export function useRowLink(): (to: string) => (event: MouseEvent<HTMLElement>) => void {
  const navigate = useNavigate();
  return useCallback(
    (to: string) => (event: MouseEvent<HTMLElement>) => {
      if (!isRowClick(event)) return;
      if (event.ctrlKey || event.metaKey || event.button === 1) {
        window.open(to, '_blank', 'noopener');
        return;
      }
      void navigate(to);
    },
    [navigate],
  );
}
