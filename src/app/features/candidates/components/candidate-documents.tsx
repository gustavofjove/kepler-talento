import { useCallback, useEffect, useRef, useState, type ChangeEvent, type FormEvent } from 'react';
import { usePermission, useServices } from '../../../core/di/services-context';
import { useErrorToast } from '../../../core/services/use-error-toast';
import type { Candidate, CandidateDocument } from '../models/candidate.models';

const ACCEPTED_FILES = '.pdf,.doc,.docx,.odt,.rtf,.txt,.jpg,.jpeg,.png,.tif,.tiff,.bmp';

function availability(document: CandidateDocument): {
  label: string;
  explanation?: string;
  downloadable: boolean;
} {
  switch (document.availabilityState) {
    case 'Pending':
      return { label: 'En análisis', downloadable: false };
    case 'Available':
      return { label: 'Disponible', downloadable: true };
    case 'Error':
      return {
        label: 'Error de análisis',
        explanation: 'No se ha podido analizar el archivo. Inténtalo de nuevo.',
        downloadable: false,
      };
    case 'Refused':
      return {
        label: 'No disponible',
        explanation: 'El archivo no ha superado el análisis de seguridad.',
        downloadable: false,
      };
    default:
      return {
        label: 'No disponible',
        explanation: 'Documento heredado sin archivo asociado.',
        downloadable: false,
      };
  }
}

export function CandidateDocuments({ candidate }: { candidate: Candidate | undefined }) {
  const { documentService, toastService, confirmDialogService } = useServices();
  const notifyError = useErrorToast();
  const [documents, setDocuments] = useState<CandidateDocument[]>(candidate?.documents ?? []);
  const [selectedFile, setSelectedFile] = useState<File | undefined>();
  const [isPrimary, setIsPrimary] = useState(true);
  const [pollingExhausted, setPollingExhausted] = useState(false);
  const fileInput = useRef<HTMLInputElement>(null);
  const polling = useRef(new Map<string, AbortController>());

  const canDownload = usePermission('download_candidate_documents');
  const canUpload = usePermission('upload_candidate_documents');

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
            notifyError(error, 'No se pudo consultar el estado del análisis.');
        })
        .finally(() => polling.current.delete(document.id));
    },
    [candidate, documentService, notifyError, replaceDocument],
  );

  const refresh = useCallback(async () => {
    if (!candidate) return;
    try {
      const current = await documentService.list(candidate.id);
      setDocuments(current);
      setPollingExhausted(false);
      current.forEach(observe);
    } catch (error) {
      notifyError(error, 'No se pudieron cargar los documentos.');
    }
  }, [candidate, documentService, notifyError, observe]);

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
      toastService.show('Archivo aceptado. El análisis de seguridad está en curso.', 'success');
      observe(uploaded);
    } catch (error) {
      notifyError(error, 'No se pudo subir el documento.');
    }
  };

  const download = async (document: CandidateDocument): Promise<void> => {
    if (!candidate) return;
    try {
      await documentService.download(candidate.id, document.id, document.originalFilename);
    } catch (error) {
      notifyError(error, 'No se pudo descargar el documento.');
    }
  };

  const markPrimary = async (documentId: string): Promise<void> => {
    if (!candidate) return;
    try {
      await documentService.setPrimary(candidate.id, documentId);
      await refresh();
      toastService.show('CV principal actualizado.', 'success');
    } catch (error) {
      notifyError(error, 'No se pudo actualizar el CV.');
    }
  };

  const remove = async (document: CandidateDocument): Promise<void> => {
    if (!candidate) return;
    const confirmed = await confirmDialogService.confirm({
      title: 'Eliminar documento',
      message: 'Se eliminará el documento seleccionado del candidato.',
      confirmText: 'Eliminar documento',
      cancelText: 'Cancelar',
      danger: true,
    });
    if (!confirmed) return;
    try {
      await documentService.remove(candidate.id, document.id);
      polling.current.get(document.id)?.abort();
      setDocuments((current) => current.filter((item) => item.id !== document.id));
      toastService.show(
        document.isPrimary
          ? 'Documento eliminado. Elija explícitamente un nuevo CV principal.'
          : 'Documento eliminado.',
        'success',
      );
    } catch (error) {
      notifyError(error, 'No se pudo eliminar el documento.');
    }
  };

  return (
    <section className="section-block" data-testid="candidate-documents">
      <h3 className="section-title">Documentos</h3>
      {!documents.length ? <p className="empty-state">Sin CV adjunto.</p> : null}
      <div className="item-list">
        {documents.map((document) => {
          const state = availability(document);
          return (
            <div className="item-row" key={document.id} data-testid="candidate-document">
              <p className="item-main">
                <strong>{document.originalFilename}</strong>
                <span className="badge">
                  {document.isPrimary ? 'Principal' : document.documentType}
                </span>
                <span className="badge" data-testid="document-availability">
                  {state.label}
                </span>
                {state.explanation ? <span>{state.explanation}</span> : null}
              </p>
              {canDownload && state.downloadable ? (
                <button
                  className="button secondary"
                  type="button"
                  onClick={() => void download(document)}
                >
                  Descargar
                </button>
              ) : null}
              {canUpload && !document.isPrimary ? (
                <button
                  className="button ghost"
                  type="button"
                  onClick={() => void markPrimary(document.id)}
                >
                  Marcar principal
                </button>
              ) : null}
              {canUpload ? (
                <button
                  className="button danger"
                  type="button"
                  onClick={() => void remove(document)}
                >
                  Eliminar
                </button>
              ) : null}
            </div>
          );
        })}
      </div>
      {pollingExhausted ? (
        <p className="empty-state">
          El análisis sigue en curso.{' '}
          <button className="button ghost" type="button" onClick={() => void refresh()}>
            Actualizar
          </button>
        </p>
      ) : null}
      {canUpload ? (
        <form className="section-block" onSubmit={upload} noValidate>
          <div className="grid two">
            <div className="field">
              <label htmlFor="file">Archivo CV</label>
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
                Marcar como CV principal
              </label>
            </div>
          </div>
          <div className="form-actions">
            <button
              className="button"
              type="submit"
              data-testid="document-upload"
              disabled={!selectedFile}
            >
              Subir CV
            </button>
          </div>
        </form>
      ) : null}
    </section>
  );
}
