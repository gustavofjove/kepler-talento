/**
 * Row CV preview (KTL-35): which CV is open on a page, and where it is shown.
 */

/**
 * The content width from which the CV sits beside the table instead of under its row. It is the
 * same threshold as the `@container page-split (min-width: 1360px)` rules in `styles.css`, which
 * lay the two columns out; keep them equal.
 */
export const CV_SPLIT_MIN_WIDTH = 1360;

export type CvPlacement = 'side' | 'inline';

/** How long the hide animation plays; keep equal to `--row-cv-close` in row-cv-preview.css. */
export const CV_CLOSE_MS = 220;

/**
 * Whether hiding a CV should wait for its animation. Not when the user prefers reduced motion,
 * nor where motion preferences cannot be read (tests), so the CV then goes at once.
 */
export function animatesClose(target: Pick<Window, 'matchMedia'> | undefined): boolean {
  return (
    typeof target?.matchMedia === 'function' &&
    !target.matchMedia('(prefers-reduced-motion: reduce)').matches
  );
}

/** The open CV: the table it was opened from, so a candidate in two tables toggles in one. */
export interface OpenRowCv {
  tableId: string;
  candidateId: string;
  name: string;
}

export function placementFor(width: number): CvPlacement {
  return width >= CV_SPLIT_MIN_WIDTH ? 'side' : 'inline';
}

export function openKey(tableId: string, candidateId: string): string {
  return `${tableId}:${candidateId}`;
}

/** The DOM id of the open CV's panel, for the row button's `aria-controls`. */
export function panelId(open: Pick<OpenRowCv, 'tableId' | 'candidateId'>): string {
  return `row-cv-${open.tableId}-${open.candidateId}`;
}

export function isOpenRow(
  open: OpenRowCv | null,
  tableId: string,
  candidateId: string,
): open is OpenRowCv {
  return open !== null && open.tableId === tableId && open.candidateId === candidateId;
}

/** Whether the open CV's row is no longer among the rows its table shows. */
export function isOpenRowGone(
  open: OpenRowCv | null,
  tableId: string,
  visibleCandidateIds: readonly string[],
): boolean {
  return (
    open !== null && open.tableId === tableId && !visibleCandidateIds.includes(open.candidateId)
  );
}
