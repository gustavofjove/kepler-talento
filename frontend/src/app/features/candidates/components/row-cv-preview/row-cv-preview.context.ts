import { createContext, useContext, useEffect } from 'react';
import { usePermission } from '../../../../core/di/services-context';
import type { PreviewContentCache } from '../candidate-cv-preview.logic';
import { isOpenRow, type CvPlacement, type OpenRowCv } from './row-cv-preview.logic';

/** What a page's `RowCvPreviewProvider` shares with its tables (KTL-35, design D4). */
export interface RowCvPreviewContextValue {
  open: OpenRowCv | null;
  /** The open CV is playing its hide animation; it already counts as hidden for its row. */
  closing: boolean;
  placement: CvPlacement;
  /** Opens that row's CV, closing any other; closes it when it is the open one. */
  toggle(row: OpenRowCv): void;
  /** Closes the open CV and puts focus back on its row's button. */
  close(): void;
  /** A table's current rows; the open CV closes when its row is no longer among them. */
  reportVisible(tableId: string, candidateIds: readonly string[]): void;
  registerButton(tableId: string, candidateId: string, element: HTMLButtonElement | null): void;
  registerRow(tableId: string, candidateId: string, element: HTMLTableRowElement | null): void;
  contentCache: PreviewContentCache;
  /** Attach to the `.page-split` element whose width decides the placement. */
  splitRef(element: HTMLDivElement | null): void;
}

export const RowCvPreviewContext = createContext<RowCvPreviewContextValue | null>(null);

export function useRowCvPreviewContext(): RowCvPreviewContextValue | null {
  return useContext(RowCvPreviewContext);
}

/**
 * A table's view of the page's row CV preview. The «CV» column exists only inside a provider and
 * for an actor holding `documents.download`; the API masks the flag too, so this is presentation,
 * not the control.
 */
export function useRowCvTable(tableId: string, visibleCandidateIds: readonly string[]) {
  const context = useRowCvPreviewContext();
  const canDownload = usePermission('documents.download');
  const reportVisible = context?.reportVisible;
  // A string key, so a new array with the same rows does not re-report.
  const visibleKey = visibleCandidateIds.join('\n');
  useEffect(() => {
    reportVisible?.(tableId, visibleKey ? visibleKey.split('\n') : []);
  }, [reportVisible, tableId, visibleKey]);

  const open = context?.open ?? null;
  return {
    enabled: context !== null && canDownload,
    isOpen: (candidateId: string): boolean =>
      isOpenRow(open, tableId, candidateId) && !context?.closing,
    rowRef:
      (candidateId: string) =>
      (element: HTMLTableRowElement | null): void =>
        context?.registerRow(tableId, candidateId, element),
  };
}
