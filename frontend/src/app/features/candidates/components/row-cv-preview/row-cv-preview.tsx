import { useCallback, useEffect, useMemo, useRef, useState, type ReactNode } from 'react';
import { useTranslation } from 'react-i18next';
import { Link } from 'react-router';
import { useServices } from '../../../../core/di/services-context';
import { useErrorToast } from '../../../../core/services/use-error-toast';
import { CandidateCvPreview } from '../candidate-cv-preview';
import type { PreviewContentCache } from '../candidate-cv-preview.logic';
import {
  RowCvPreviewContext,
  useRowCvPreviewContext,
  type RowCvPreviewContextValue,
} from './row-cv-preview.context';
import {
  isOpenRow,
  animatesClose,
  CV_CLOSE_MS,
  isOpenRowGone,
  openKey,
  panelId,
  placementFor,
  type CvPlacement,
  type OpenRowCv,
} from './row-cv-preview.logic';
import './row-cv-preview.css';

/** Marks the CV area, so focus inside it can be followed when it moves or closes. */
const PANEL_ATTRIBUTE = 'data-row-cv-panel';

/**
 * Owns the one CV a page may show from its candidate tables (KTL-35): which row is open, where
 * it is shown, and its downloaded content. The placement is measured on the `.page-split`
 * element rendered by `RowCvSplit` — the content area, never the window (design D5).
 */
export function RowCvPreviewProvider({ children }: { children: ReactNode }) {
  const [open, setOpen] = useState<OpenRowCv | null>(null);
  const [placement, setPlacement] = useState<CvPlacement>('inline');
  const openRef = useRef(open);
  const placementRef = useRef(placement);
  const buttons = useRef(new Map<string, HTMLButtonElement>());
  const rows = useRef(new Map<string, HTMLTableRowElement>());
  // One entry: the open CV's content, kept only so a move between placements does not fetch
  // again (design D6). Dropped whenever another CV opens or it closes.
  const cached = useRef<{ documentId: string; blob: Blob } | null>(null);
  const focusAfterMove = useRef(false);
  const observer = useRef<ResizeObserver | null>(null);
  // While set, the open CV is playing its hide animation and is removed when it ends.
  const [closing, setClosing] = useState(false);
  const closeTimer = useRef<number | undefined>(undefined);

  const show = useCallback((next: OpenRowCv | null) => {
    window.clearTimeout(closeTimer.current);
    closeTimer.current = undefined;
    setClosing(false);
    const current = openRef.current;
    if (
      !next ||
      !current ||
      openKey(current.tableId, current.candidateId) !== openKey(next.tableId, next.candidateId)
    ) {
      cached.current = null;
    }
    openRef.current = next;
    setOpen(next);
  }, []);

  /**
   * Hides the open CV the user asked to hide: it plays its hide animation first, unless the user
   * prefers reduced motion. Anything else that removes it (another CV, its row leaving) is
   * immediate, so the page never waits on an animation.
   */
  const hide = useCallback(() => {
    if (!openRef.current || closeTimer.current !== undefined) return;
    if (!animatesClose(window)) {
      show(null);
      return;
    }
    setClosing(true);
    closeTimer.current = window.setTimeout(() => show(null), CV_CLOSE_MS);
  }, [show]);

  const toggle = useCallback(
    (row: OpenRowCv) => {
      const current = openRef.current;
      if (isOpenRow(current, row.tableId, row.candidateId) && closeTimer.current === undefined) {
        hide();
        return;
      }
      show(row);
    },
    [hide, show],
  );

  const close = useCallback(() => {
    const current = openRef.current;
    if (!current) return;
    hide();
    buttons.current.get(openKey(current.tableId, current.candidateId))?.focus();
  }, [hide]);

  useEffect(() => () => window.clearTimeout(closeTimer.current), []);

  const reportVisible = useCallback(
    (tableId: string, candidateIds: readonly string[]) => {
      if (isOpenRowGone(openRef.current, tableId, candidateIds)) show(null);
    },
    [show],
  );

  const registerButton = useCallback(
    (tableId: string, candidateId: string, element: HTMLButtonElement | null) => {
      const key = openKey(tableId, candidateId);
      if (element) buttons.current.set(key, element);
      else buttons.current.delete(key);
    },
    [],
  );

  const registerRow = useCallback(
    (tableId: string, candidateId: string, element: HTMLTableRowElement | null) => {
      const key = openKey(tableId, candidateId);
      if (element) rows.current.set(key, element);
      else rows.current.delete(key);
    },
    [],
  );

  const contentCache = useMemo<PreviewContentCache>(
    () => ({
      get: (documentId) =>
        cached.current?.documentId === documentId ? cached.current.blob : undefined,
      set: (documentId, blob) => {
        cached.current = { documentId, blob };
      },
    }),
    [],
  );

  const splitRef = useCallback((element: HTMLDivElement | null) => {
    observer.current?.disconnect();
    observer.current = null;
    if (!element || typeof ResizeObserver === 'undefined') return;
    const resizeObserver = new ResizeObserver((entries) => {
      const width = entries[0]?.contentRect.width ?? element.clientWidth;
      const next = placementFor(width);
      if (next === placementRef.current) return;
      // Read before the move unmounts the CV and focus falls back to the page.
      focusAfterMove.current = Boolean(document.activeElement?.closest(`[${PANEL_ATTRIBUTE}]`));
      placementRef.current = next;
      setPlacement(next);
    });
    resizeObserver.observe(element);
    observer.current = resizeObserver;
  }, []);

  useEffect(() => () => observer.current?.disconnect(), []);

  // After a move: into the table, bring the row into view; either way, focus that was inside the
  // CV goes to the row's button rather than being lost.
  useEffect(() => {
    const current = openRef.current;
    if (!current) return;
    const key = openKey(current.tableId, current.candidateId);
    if (placement === 'inline') rows.current.get(key)?.scrollIntoView?.({ block: 'nearest' });
    if (focusAfterMove.current) {
      focusAfterMove.current = false;
      buttons.current.get(key)?.focus();
    }
  }, [placement]);

  const value = useMemo<RowCvPreviewContextValue>(
    () => ({
      open,
      closing,
      placement,
      toggle,
      close,
      reportVisible,
      registerButton,
      registerRow,
      contentCache,
      splitRef,
    }),
    [
      open,
      closing,
      placement,
      toggle,
      close,
      reportVisible,
      registerButton,
      registerRow,
      contentCache,
      splitRef,
    ],
  );

  return <RowCvPreviewContext.Provider value={value}>{children}</RowCvPreviewContext.Provider>;
}

/**
 * The KTL-28 split around a page's tables: the tables in the main column, the open CV in the
 * aside when there is room. The existing `.page-split` rules only split the grid and widen the
 * shell while the aside holds a CV.
 */
export function RowCvSplit({ children }: { children: ReactNode }) {
  const context = useRowCvPreviewContext();
  const open = context?.open ?? null;
  return (
    <div className="page-split" ref={context?.splitRef}>
      <div className="page-split__layout">
        <div className="page-split__main">{children}</div>
        <aside className="page-split__aside">
          {open && context?.placement === 'side' ? <RowCvPanel open={open} /> : null}
        </aside>
      </div>
    </div>
  );
}

/** «Ver» / «Ocultar» in a row's «CV» cell. */
export function RowCvButton({
  tableId,
  candidateId,
  name,
}: {
  tableId: string;
  candidateId: string;
  name: string;
}) {
  const { t } = useTranslation();
  const context = useRowCvPreviewContext();
  if (!context) return null;
  // A CV playing its hide animation already counts as hidden for its button.
  const isOpen = isOpenRow(context.open, tableId, candidateId) && !context.closing;
  return (
    <button
      className="button ghost small row-cv-toggle"
      type="button"
      name="toggleCv"
      data-testid="row-cv-toggle"
      aria-expanded={isOpen}
      aria-controls={isOpen ? panelId({ tableId, candidateId }) : undefined}
      aria-label={t(isOpen ? 'cvPanel.hideLabel' : 'cvPanel.showLabel', { name })}
      title={t(isOpen ? 'cvPanel.hideLabel' : 'cvPanel.showLabel', { name })}
      ref={(element) => context.registerButton(tableId, candidateId, element)}
      onClick={() => context.toggle({ tableId, candidateId, name })}
    >
      <EyeIcon crossed={isOpen} />
      {/* Where the CV goes: «Ver» points to where it opens (beside the table or under the row),
          «Ocultar» back the other way. Decorative; the accessible name says it all. */}
      <span className="row-cv-toggle__arrow" aria-hidden="true">
        {context.placement === 'side' ? (isOpen ? '←' : '→') : isOpen ? '↑' : '↓'}
      </span>
    </button>
  );
}

/**
 * Downloads the candidate's primary CV from a row (KTL-35), whether or not it can be previewed.
 * It reads the candidate's document list first, as the preview does, so no document id travels
 * in the list responses; the download itself is the usual permission-checked, audited response.
 */
export function RowCvDownloadButton({ candidateId, name }: { candidateId: string; name: string }) {
  const { t } = useTranslation();
  const { documentService, toastService } = useServices();
  const notifyError = useErrorToast();
  const [busy, setBusy] = useState(false);
  const label = t('cvPanel.downloadLabel', { name });

  const download = async (): Promise<void> => {
    setBusy(true);
    try {
      const primary = (await documentService.list(candidateId)).find(
        (document) => document.isPrimary && document.availabilityState === 'Available',
      );
      // The flag went stale since the list loaded (the CV was replaced or removed).
      if (!primary) {
        toastService.show(t('cvPanel.downloadUnavailable'), 'warning');
        return;
      }
      await documentService.download(candidateId, primary.id, primary.originalFilename);
    } catch (error) {
      notifyError(error, t('cvPanel.downloadFailed'));
    } finally {
      setBusy(false);
    }
  };

  return (
    <button
      className="button ghost small row-cv-toggle"
      type="button"
      name="downloadCv"
      data-testid="row-cv-download"
      aria-label={label}
      title={label}
      disabled={busy}
      onClick={() => void download()}
    >
      <DiskIcon />
    </button>
  );
}

/**
 * A floppy disk for «Descargar». Decorative: the button carries the name. Drawn on the 16px grid
 * it is shown at, with 1px strokes on half-pixel coordinates, so the long outer edges render as
 * crisp single pixels instead of smearing across two and looking heavier than the inner lines.
 */
function DiskIcon() {
  return (
    <svg
      className="row-cv-toggle__icon"
      viewBox="0 0 16 16"
      width="16"
      height="16"
      aria-hidden="true"
      focusable="false"
      fill="none"
      stroke="currentColor"
      strokeWidth={1}
      strokeLinecap="round"
      strokeLinejoin="round"
    >
      <path d="M2.5 1.5h8.5l3.5 3.5v8.5a1 1 0 0 1-1 1h-11a1 1 0 0 1-1-1v-11a1 1 0 0 1 1-1z" />
      <path d="M4.5 1.5v3.5h6v-3.5" />
      <path d="M4.5 14.5v-5a1 1 0 0 1 1-1h5a1 1 0 0 1 1 1v5" />
    </svg>
  );
}

/**
 * The «CV» cell's content: the download (left) and the preview toggle, each only when the API
 * reports the primary CV can be downloaded or previewed.
 */
export function RowCvActions({
  tableId,
  candidateId,
  name,
  downloadable,
  previewable,
}: {
  tableId: string;
  candidateId: string;
  name: string;
  downloadable: boolean;
  previewable: boolean;
}) {
  if (!downloadable && !previewable) return null;
  return (
    <div className="row-cv-actions">
      {downloadable ? <RowCvDownloadButton candidateId={candidateId} name={name} /> : null}
      {previewable ? <RowCvButton tableId={tableId} candidateId={candidateId} name={name} /> : null}
    </div>
  );
}

/** An eye for «Ver», crossed out for «Ocultar». Decorative: the button carries the name. */
function EyeIcon({ crossed }: { crossed: boolean }) {
  return (
    <svg
      className="row-cv-toggle__icon"
      viewBox="0 0 24 24"
      width="16"
      height="16"
      aria-hidden="true"
      focusable="false"
      fill="none"
      stroke="currentColor"
      strokeWidth={1.5}
      strokeLinecap="round"
      strokeLinejoin="round"
    >
      <path d="M2 12s3.5-7 10-7 10 7 10 7-3.5 7-10 7S2 12 2 12z" />
      <circle cx="12" cy="12" r="3" />
      {crossed ? <path d="M3 3l18 18" /> : null}
    </svg>
  );
}

/** The row under an open candidate when the CV is shown inside the table. */
export function RowCvInlineRow({
  tableId,
  candidateId,
  colSpan,
}: {
  tableId: string;
  candidateId: string;
  colSpan: number;
}) {
  const context = useRowCvPreviewContext();
  const open = context?.open ?? null;
  if (context?.placement !== 'inline' || !isOpenRow(open, tableId, candidateId)) return null;
  return (
    <tr className="cv-row" data-testid="row-cv-row">
      <td colSpan={colSpan}>
        <div className={context.closing ? 'cv-row__content is-closing' : 'cv-row__content'}>
          <RowCvPanel open={open} />
        </div>
      </td>
    </tr>
  );
}

function RowCvPanel({ open }: { open: OpenRowCv }) {
  const { t } = useTranslation();
  const context = useRowCvPreviewContext();
  if (!context) return null;
  const { name, candidateId } = open;
  return (
    <div
      className={context.closing ? 'row-cv-panel is-closing' : 'row-cv-panel'}
      data-testid="row-cv-panel"
      {...{ [PANEL_ATTRIBUTE]: '' }}
    >
      <CandidateCvPreview
        key={openKey(open.tableId, candidateId)}
        id={panelId(open)}
        candidateId={candidateId}
        contentCache={context.contentCache}
        emptyMessage={t('cvPanel.empty')}
        header={
          <div className="row-cv-panel__header">
            <h2>{t('cvPanel.title', { name })}</h2>
            <div className="form-actions">
              <Link
                className="button ghost small"
                to={`/app/candidates/${candidateId}`}
                data-testid="row-cv-open-profile"
              >
                {t('cvPanel.openProfile')}
              </Link>
              <button
                className="button secondary small"
                type="button"
                name="hideCv"
                data-testid="row-cv-hide"
                aria-label={t('cvPanel.hideLabel', { name })}
                onClick={context.close}
              >
                {t('cvPanel.hide')}
              </button>
            </div>
          </div>
        }
      />
    </div>
  );
}
