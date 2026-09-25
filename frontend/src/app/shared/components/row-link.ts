import { useCallback, type MouseEvent } from 'react';
import { useNavigate } from 'react-router';
import './row-link.css';

/** Clicks on these keep their own behaviour: a row never hijacks a real control. */
const INTERACTIVE = 'a, button, input, select, textarea, label, [role="button"]';

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
      if (event.defaultPrevented || (event.button !== 0 && event.button !== 1)) return;
      if ((event.target as Element).closest(INTERACTIVE)) return;
      if (window.getSelection()?.toString()) return;
      if (event.ctrlKey || event.metaKey || event.button === 1) {
        window.open(to, '_blank', 'noopener');
        return;
      }
      void navigate(to);
    },
    [navigate],
  );
}
