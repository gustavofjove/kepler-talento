import { useEffect, useRef, useState, type ReactNode } from 'react';
import { useTranslation } from 'react-i18next';
import { usePermission, useServices } from '../../../core/di/services-context';
import { useErrorToast } from '../../../core/services/use-error-toast';
import type { CandidateDocument } from '../models/candidate.models';
import {
  isPreviewable,
  pickDefaultDocument,
  previewMessageKey,
  viewerUrl,
  type PreviewContentCache,
} from './candidate-cv-preview.logic';
import './candidate-cv-preview.css';

type LoadState = 'idle' | 'loading' | 'ready' | 'error';

interface CandidateCvPreviewProps {
  candidateId: string;
  /**
   * The documents the caller already holds (the candidate page's aggregate). They render at once
   * while the component re-reads the list. Without them the preview shows a loading state until
   * the list arrives.
   */
  initialDocuments?: readonly CandidateDocument[];
  /** Replaces the default «Vista previa del CV» heading (the table rows' «CV de …», KTL-35). */
  header?: ReactNode;
  /**
   * Keeps downloaded content outside the component, so a remount (a row CV moving between the
   * side panel and the table) shows it again without a new request. Defaults to a per-instance
   * cache.
   */
  contentCache?: PreviewContentCache;
  /**
   * Shown when the candidate has no document to preview. Without it the component renders
   * nothing, as on the candidate page; a row CV opened on a stale flag must never be empty.
   */
  emptyMessage?: string;
  /**
   * The candidate page's rule (KTL-35): render nothing, and request no content, unless the
   * primary document in the live list can be previewed. A pending primary appears once it
   * settles clean.
   */
  requirePreviewablePrimary?: boolean;
  /** Id of the panel element, for a controlling button's `aria-controls`. */
  id?: string;
}

export function CandidateCvPreview({
  candidateId,
  initialDocuments,
  header,
  contentCache,
  emptyMessage,
  requirePreviewablePrimary = false,
  id,
}: CandidateCvPreviewProps) {
  const { t } = useTranslation();
  const { documentService } = useServices();
  const notifyError = useErrorToast();
  const canDownload = usePermission('documents.download');
  const [documents, setDocuments] = useState<readonly CandidateDocument[]>(initialDocuments ?? []);
  // Seeded documents count as a known list; otherwise the first read is still to come.
  const [listLoaded, setListLoaded] = useState(initialDocuments !== undefined);
  const initial = pickDefaultDocument(documents);
  const [selectedId, setSelectedId] = useState(initial?.id ?? '');
  const [objectUrl, setObjectUrl] = useState<string>();
  const [loadState, setLoadState] = useState<LoadState>('idle');
  const [retry, setRetry] = useState(0);
  const ownCache = useRef(new Map<string, Blob>());
  const cache: PreviewContentCache = contentCache ?? {
    get: (documentId) => ownCache.current.get(documentId),
    set: (documentId, blob) => void ownCache.current.set(documentId, blob),
  };
  const cacheRef = useRef(cache);
  cacheRef.current = cache;
  const previousCandidateId = useRef(candidateId);

  useEffect(() => {
    const candidateChanged = previousCandidateId.current !== candidateId;
    previousCandidateId.current = candidateId;
    const seed = initialDocuments ?? [];
    if (initialDocuments !== undefined || candidateChanged) {
      setDocuments(seed);
      setListLoaded(initialDocuments !== undefined);
      setSelectedId((selected) =>
        !candidateChanged && seed.some((document) => document.id === selected)
          ? selected
          : (pickDefaultDocument(seed)?.id ?? ''),
      );
    }
    if (candidateChanged || !canDownload) {
      setObjectUrl(undefined);
      setLoadState('idle');
      ownCache.current.clear();
    }
    if (!canDownload) return;
    const controller = new AbortController();
    void documentService
      .list(candidateId)
      .then((current) => {
        if (controller.signal.aborted) return;
        setDocuments(current);
        setListLoaded(true);
        setSelectedId((selected) =>
          current.some((document) => document.id === selected)
            ? selected
            : (pickDefaultDocument(current)?.id ?? ''),
        );
        current
          .filter((document) => document.availabilityState === 'Pending')
          .forEach((document) => {
            void documentService
              .observeUntilSettled(
                candidateId,
                document.id,
                (updated) => {
                  setDocuments((documents) =>
                    documents.map((item) => (item.id === updated.id ? updated : item)),
                  );
                  // A primary that settles clean takes the selection, so the candidate page's
                  // gate (KTL-35) opens on the primary; otherwise a choice already made stays.
                  setSelectedId((selected) => {
                    if (selected && !(updated.isPrimary && isPreviewable(updated))) return selected;
                    return isPreviewable(updated) ? updated.id : selected;
                  });
                },
                controller.signal,
              )
              .catch((error) => {
                if (!controller.signal.aborted)
                  notifyError(error, t('candidate.profile.preview.failure'));
              });
          });
      })
      .catch((error) => {
        if (controller.signal.aborted) return;
        setListLoaded(true);
        notifyError(error, t('candidate.profile.preview.failure'));
      });
    return () => controller.abort();
  }, [canDownload, candidateId, initialDocuments, documentService, notifyError, t]);

  const primary = documents.find((document) => document.isPrimary);
  const gateOpen = !requirePreviewablePrimary || (primary !== undefined && isPreviewable(primary));
  const selected = documents.find((document) => document.id === selectedId);
  const selectedDocumentId = selected?.id;
  const selectedFilename = selected?.originalFilename;
  const selectedIsPreviewable = gateOpen && selected ? isPreviewable(selected) : false;

  useEffect(() => {
    if (!canDownload || !selectedDocumentId || !selectedFilename || !selectedIsPreviewable) {
      setObjectUrl(undefined);
      setLoadState('idle');
      return;
    }
    const controller = new AbortController();
    let activeUrl: string | undefined;
    const showBlob = (blob: Blob) => {
      activeUrl = URL.createObjectURL(blob);
      setObjectUrl(activeUrl);
      setLoadState('ready');
    };
    const cached = cacheRef.current.get(selectedDocumentId);
    if (cached) {
      showBlob(cached);
      return () => URL.revokeObjectURL(activeUrl!);
    }
    setObjectUrl(undefined);
    setLoadState('loading');
    void documentService
      .openPreview(candidateId, selectedDocumentId, selectedFilename, controller.signal)
      .then((download) => {
        if (controller.signal.aborted) return;
        cacheRef.current.set(selectedDocumentId, download.blob);
        showBlob(download.blob);
      })
      .catch((error) => {
        if (controller.signal.aborted) return;
        setLoadState('error');
        notifyError(error, t('candidate.profile.preview.failure'));
      });
    return () => {
      controller.abort();
      if (activeUrl) URL.revokeObjectURL(activeUrl);
    };
  }, [
    canDownload,
    candidateId,
    documentService,
    notifyError,
    retry,
    selectedDocumentId,
    selectedFilename,
    selectedIsPreviewable,
    t,
  ]);

  if (!canDownload || !gateOpen) return null;

  const heading = header ?? <h2>{t('candidate.profile.preview.title')}</h2>;

  if (!documents.length || !selected) {
    if (emptyMessage === undefined) return null;
    return (
      <section className="panel cv-preview" data-testid="candidate-cv-preview" id={id}>
        {heading}
        {listLoaded ? (
          <p className="empty-state" data-testid="cv-preview-empty">
            {emptyMessage}
          </p>
        ) : (
          <p className="empty-state" aria-busy="true">
            {t('candidate.profile.preview.loading')}
          </p>
        )}
      </section>
    );
  }

  const download = async (document: CandidateDocument): Promise<void> => {
    try {
      await documentService.download(candidateId, document.id, document.originalFilename);
    } catch (error) {
      notifyError(error, t('candidate.profile.preview.downloadFailure'));
    }
  };

  return (
    <section className="panel cv-preview" data-testid="candidate-cv-preview" id={id}>
      {heading}
      {documents.length > 1 ? (
        <div className="field cv-preview__picker">
          <label htmlFor="preview-document">{t('candidate.profile.preview.document')}</label>
          <select
            id="preview-document"
            name="previewDocument"
            data-testid="preview-document-select"
            value={selectedId}
            onChange={(event) => setSelectedId(event.target.value)}
          >
            {documents.map((document) => (
              <option key={document.id} value={document.id}>
                {document.originalFilename}
              </option>
            ))}
          </select>
        </div>
      ) : null}
      {loadState === 'loading' ? (
        <p className="empty-state" aria-busy="true">
          {t('candidate.profile.preview.loading')}
        </p>
      ) : null}
      {loadState === 'error' ? (
        <div className="empty-state" role="alert">
          <p>{t('candidate.profile.preview.failure')}</p>
          <button
            className="button secondary"
            type="button"
            onClick={() => setRetry((value) => value + 1)}
          >
            {t('candidate.profile.preview.retry')}
          </button>
        </div>
      ) : null}
      {!isPreviewable(selected) ? (
        <div className="empty-state" data-testid="cv-preview-unsupported">
          <p>{t(previewMessageKey(selected))}</p>
          {selected.availabilityState === 'Available' ? (
            <button
              className="button secondary"
              type="button"
              onClick={() => void download(selected)}
            >
              {t('candidate.profile.preview.download')}
            </button>
          ) : null}
        </div>
      ) : null}
      {loadState === 'ready' && objectUrl ? (
        <object
          type="application/pdf"
          data={viewerUrl(objectUrl)}
          data-testid="cv-preview-viewer"
          aria-label={t('candidate.profile.preview.viewerLabel')}
          title={t('candidate.profile.preview.viewerLabel')}
          className="cv-preview__viewer"
        >
          <p>{t('candidate.profile.preview.fallback')}</p>
          <button
            className="button secondary"
            type="button"
            onClick={() => void download(selected)}
          >
            {t('candidate.profile.preview.download')}
          </button>
        </object>
      ) : null}
    </section>
  );
}
