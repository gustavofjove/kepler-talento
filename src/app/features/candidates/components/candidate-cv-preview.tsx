import { useEffect, useRef, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { usePermission, useServices } from '../../../core/di/services-context';
import { useErrorToast } from '../../../core/services/use-error-toast';
import type { Candidate, CandidateDocument } from '../models/candidate.models';
import {
  isPreviewable,
  pickDefaultDocument,
  previewMessageKey,
} from './candidate-cv-preview.logic';
import './candidate-cv-preview.css';

type LoadState = 'idle' | 'loading' | 'ready' | 'error';

export function CandidateCvPreview({ candidate }: { candidate: Candidate }) {
  const { t } = useTranslation();
  const { documentService } = useServices();
  const notifyError = useErrorToast();
  const canDownload = usePermission('documents.download');
  const [documents, setDocuments] = useState(candidate.documents);
  const initial = pickDefaultDocument(documents);
  const [selectedId, setSelectedId] = useState(initial?.id ?? '');
  const [objectUrl, setObjectUrl] = useState<string>();
  const [loadState, setLoadState] = useState<LoadState>('idle');
  const [retry, setRetry] = useState(0);
  const blobCache = useRef(new Map<string, Blob>());

  useEffect(() => {
    setDocuments(candidate.documents);
    setSelectedId(pickDefaultDocument(candidate.documents)?.id ?? '');
    setObjectUrl(undefined);
    setLoadState('idle');
    blobCache.current.clear();
    if (!canDownload) return;
    const controller = new AbortController();
    void documentService
      .list(candidate.id)
      .then((current) => {
        if (controller.signal.aborted) return;
        setDocuments(current);
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
                candidate.id,
                document.id,
                (updated) => {
                  setDocuments((documents) =>
                    documents.map((item) => (item.id === updated.id ? updated : item)),
                  );
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
        if (!controller.signal.aborted) notifyError(error, t('candidate.profile.preview.failure'));
      });
    return () => controller.abort();
  }, [canDownload, candidate.id, candidate.documents, documentService, notifyError, t]);

  const selected = documents.find((document) => document.id === selectedId);
  const selectedDocumentId = selected?.id;
  const selectedFilename = selected?.originalFilename;
  const selectedIsPreviewable = selected ? isPreviewable(selected) : false;

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
    const cached = blobCache.current.get(selectedDocumentId);
    if (cached) {
      showBlob(cached);
      return () => URL.revokeObjectURL(activeUrl!);
    }
    setObjectUrl(undefined);
    setLoadState('loading');
    void documentService
      .openPreview(candidate.id, selectedDocumentId, selectedFilename, controller.signal)
      .then((download) => {
        if (controller.signal.aborted) return;
        blobCache.current.set(selectedDocumentId, download.blob);
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
    candidate.id,
    documentService,
    notifyError,
    retry,
    selectedDocumentId,
    selectedFilename,
    selectedIsPreviewable,
    t,
  ]);

  if (!canDownload || !documents.length || !selected) return null;

  const download = async (document: CandidateDocument): Promise<void> => {
    try {
      await documentService.download(candidate.id, document.id, document.originalFilename);
    } catch (error) {
      notifyError(error, t('candidate.profile.preview.downloadFailure'));
    }
  };

  return (
    <section className="panel span-all cv-preview" data-testid="candidate-cv-preview">
      <h2>{t('candidate.profile.preview.title')}</h2>
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
          data={objectUrl}
          data-testid="cv-preview-viewer"
          aria-label={t('candidate.profile.preview.viewerLabel')}
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
