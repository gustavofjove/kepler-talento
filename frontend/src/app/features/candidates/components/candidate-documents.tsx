import { useCallback, useEffect, useRef, useState, type ChangeEvent, type FormEvent } from 'react';
import { useTranslation } from 'react-i18next';
import { usePermission, useServices } from '../../../core/di/services-context';
import { useErrorToast } from '../../../core/services/use-error-toast';
import type { Candidate, CandidateDocument } from '../models/candidate.models';

const ACCEPTED_FILES = '.pdf,.doc,.docx,.odt,.rtf,.txt,.jpg,.jpeg,.png,.tif,.tiff,.bmp';

/** i18n keys under `candidate.profile.documents.` for a document's availability state. */
function availability(document: CandidateDocument): {
  label: string;
  explanation?: string;
  downloadable: boolean;
} {
  switch (document.availabilityState) {
    case 'Pending':
      return { label: 'state.pending', downloadable: false };
    case 'Available':
      return { label: 'state.available', downloadable: true };
    case 'Error':
      return { label: 'state.error', explanation: 'explanation.error', downloadable: false };
    case 'Refused':
      return {
        label: 'state.unavailable',
        explanation: 'explanation.refused',
        downloadable: false,
      };
    default:
      return { label: 'state.unavailable', explanation: 'explanation.legacy', downloadable: false };
  }
}

interface Props {
  candidate: Candidate | undefined;
  /** Hides upload, primary and removal; the list and download stay. See candidate-languages. */
  readOnly?: boolean;
}

export function CandidateDocuments({ candidate, readOnly = false }: Props) {
  const { t } = useTranslation();
  const { documentService, toastService, confirmDialogService } = useServices();
  const notifyError = useErrorToast();
  const [documents, setDocuments] = useState<CandidateDocument[]>(candidate?.documents ?? []);
  const [selectedFile, setSelectedFile] = useState<File | undefined>();
  const [isPrimary, setIsPrimary] = useState(true);
  const [pollingExhausted, setPollingExhausted] = useState(false);
  const [uploadError, setUploadError] = useState<string | undefined>();
  const fileInput = useRef<HTMLInputElement>(null);
  const polling = useRef(new Map<string, AbortController>());

  const canDownload = usePermission('documents.download');
  const mayUpload = usePermission('documents.upload');
  const canUpload = !readOnly && mayUpload;

  const replaceDocument = useCallback((next: CandidateDocument) => {
    setDocuments((current) => current.map((item) => (item.id === next.id ? next : item)));
  }, []);

  const observe = useCallback(
    (document: CandidateDocument) => {
      if (
        !candidate ||
        document.availabilityState !== 'Pending' ||
        polling.current.has(document.id)
      )
        return;
      const controller = new AbortController();
      polling.current.set(document.id, controller);
      void documentService
        .observeUntilSettled(candidate.id, document.id, replaceDocument, controller.signal)
        .then((settled) => {
          if (!settled && !controller.signal.aborted) setPollingExhausted(true);
        })
        .catch((error) => {
          if (!controller.signal.aborted)
            notifyError(error, t('candidate.profile.documents.pollFailure'));
        })
        .finally(() => polling.current.delete(document.id));
    },
    [candidate, documentService, notifyError, replaceDocument, t],
  );

  const refresh = useCallback(async () => {
    if (!candidate) return;
    try {
      const current = await documentService.list(candidate.id);
      setDocuments(current);
      setPollingExhausted(false);
      current.forEach(observe);
    } catch (error) {
      notifyError(error, t('candidate.profile.documents.loadFailure'));
    }
  }, [candidate, documentService, notifyError, observe, t]);

  useEffect(() => {
    void refresh();
    const active = polling.current;
    return () => {
      active.forEach((controller) => controller.abort());
      active.clear();
    };
  }, [refresh]);

  const onFileSelected = (event: ChangeEvent<HTMLInputElement>): void => {
    setSelectedFile(event.target.files?.[0]);
  };

  const upload = async (event: FormEvent<HTMLFormElement>): Promise<void> => {
    event.preventDefault();
    if (!candidate || !selectedFile) return;
    setUploadError(undefined);
    try {
      const uploaded = await documentService.upload({
        candidateId: candidate.id,
        file: selectedFile,
        isPrimary,
      });
      setDocuments((current) => [
        uploaded,
        ...current.map((item) => (isPrimary ? { ...item, isPrimary: false } : item)),
      ]);
      setSelectedFile(undefined);
      setIsPrimary(true);
      if (fileInput.current) fileInput.current.value = '';
      toastService.show(t('candidate.profile.documents.uploaded'), 'success');
      observe(uploaded);
    } catch (error) {
      setUploadError(notifyError(error, t('candidate.profile.documents.uploadFailure')));
    }
  };

  const download = async (document: CandidateDocument): Promise<void> => {
    if (!candidate) return;
    try {
      await documentService.download(candidate.id, document.id, document.originalFilename);
    } catch (error) {
      notifyError(error, t('candidate.profile.documents.downloadFailure'));
    }
  };

  const markPrimary = async (documentId: string): Promise<void> => {
    if (!candidate) return;
    try {
      await documentService.setPrimary(candidate.id, documentId);
      await refresh();
      toastService.show(t('candidate.profile.documents.primaryUpdated'), 'success');
    } catch (error) {
      notifyError(error, t('candidate.profile.documents.primaryFailure'));
    }
  };

  const remove = async (document: CandidateDocument): Promise<void> => {
    if (!candidate) return;
    const confirmed = await confirmDialogService.confirm({
      title: t('candidate.profile.documents.removeTitle'),
      message: t('candidate.profile.documents.removeMessage'),
      confirmText: t('candidate.profile.documents.removeTitle'),
      cancelText: t('candidate.detail.cancel'),
      danger: true,
    });
    if (!confirmed) return;
    try {
      await documentService.remove(candidate.id, document.id);
      polling.current.get(document.id)?.abort();
      setDocuments((current) => current.filter((item) => item.id !== document.id));
      toastService.show(
        t(
          document.isPrimary
            ? 'candidate.profile.documents.removedPrimary'
            : 'candidate.profile.documents.removed',
        ),
        'success',
      );
    } catch (error) {
      notifyError(error, t('candidate.profile.documents.removeFailure'));
    }
  };

  return (
    <section className="section-block" data-testid="candidate-documents">
      <h3 className="section-title">{t('candidate.profile.documents.title')}</h3>
      {!documents.length ? (
        <p className="empty-state">{t('candidate.profile.documents.empty')}</p>
      ) : null}
      <div className="item-list">
        {documents.map((document) => {
          const state = availability(document);
          return (
            <div className="item-row" key={document.id} data-testid="candidate-document">
              <p className="item-main">
                <strong>{document.originalFilename}</strong>
                <span className="badge">
                  {document.isPrimary
                    ? t('candidate.profile.documents.primary')
                    : document.documentType}
                </span>
                <span className="badge" data-testid="document-availability">
                  {t(`candidate.profile.documents.${state.label}`)}
                </span>
                {state.explanation ? (
                  <span>{t(`candidate.profile.documents.${state.explanation}`)}</span>
                ) : null}
              </p>
              {canDownload && state.downloadable ? (
                <button
                  className="button secondary"
                  type="button"
                  onClick={() => void download(document)}
                >
                  {t('candidate.profile.documents.download')}
                </button>
              ) : null}
              {canUpload && !document.isPrimary ? (
                <button
                  className="button ghost"
                  type="button"
                  onClick={() => void markPrimary(document.id)}
                >
                  {t('candidate.profile.documents.markPrimary')}
                </button>
              ) : null}
              {canUpload ? (
                <button
                  className="button danger"
                  type="button"
                  onClick={() => void remove(document)}
                >
                  {t('candidate.profile.documents.remove')}
                </button>
              ) : null}
            </div>
          );
        })}
      </div>
      {pollingExhausted ? (
        <p className="empty-state">
          {t('candidate.profile.documents.stillScanning')}{' '}
          <button className="button ghost" type="button" onClick={() => void refresh()}>
            {t('candidate.profile.documents.refresh')}
          </button>
        </p>
      ) : null}
      {canUpload ? (
        <form className="section-block" onSubmit={upload} noValidate>
          <div className="grid two">
            <div className="field">
              <label htmlFor="file">{t('candidate.profile.documents.file')}</label>
              <input
                id="file"
                ref={fileInput}
                name="file"
                data-testid="document-file"
                type="file"
                accept={ACCEPTED_FILES}
                onChange={onFileSelected}
              />
            </div>
            <div className="field">
              <label className="inline-check">
                <input
                  name="isPrimary"
                  data-testid="document-is-primary"
                  type="checkbox"
                  checked={isPrimary}
                  onChange={(event) => setIsPrimary(event.target.checked)}
                />
                {t('candidate.profile.documents.isPrimary')}
              </label>
            </div>
          </div>
          {uploadError ? (
            <p className="empty-state" role="alert" data-testid="document-upload-error">
              {uploadError}
            </p>
          ) : null}
          <div className="form-actions">
            <button
              className="button"
              type="submit"
              data-testid="document-upload"
              disabled={!selectedFile}
            >
              {t('candidate.profile.documents.upload')}
            </button>
          </div>
        </form>
      ) : null}
    </section>
  );
}
