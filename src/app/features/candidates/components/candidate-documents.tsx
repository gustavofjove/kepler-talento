import { useRef, useState, type ChangeEvent, type FormEvent } from 'react';
import { usePermission, useServices } from '../../../core/di/services-context';
import type { Candidate } from '../models/candidate.models';

export function CandidateDocuments({ candidate }: { candidate: Candidate | undefined }) {
  const { documentService, toastService, confirmDialogService } = useServices();
  const [selectedFile, setSelectedFile] = useState<File | undefined>(undefined);
  const [isPrimary, setIsPrimary] = useState(true);
  const [error, setError] = useState('');
  const fileInput = useRef<HTMLInputElement>(null);

  const canDownload = usePermission('download_candidate_documents');
  const canUpload = usePermission('upload_candidate_documents');

  const onFileSelected = (event: ChangeEvent<HTMLInputElement>): void => {
    setSelectedFile(event.target.files?.[0]);
  };

  // Document metadata persists through the API now, so these must be awaited: a
  // synchronous try/catch around a promise catches nothing.
  const upload = async (event: FormEvent<HTMLFormElement>): Promise<void> => {
    event.preventDefault();
    if (!candidate || !selectedFile) {
      return;
    }
    try {
      await documentService.upload({ candidateId: candidate.id, file: selectedFile, isPrimary });
      setSelectedFile(undefined);
      setIsPrimary(true);
      setError('');
      // The file input is uncontrolled; clear it so the same file can be re-picked.
      if (fileInput.current) {
        fileInput.current.value = '';
      }
      toastService.show('CV subido correctamente.', 'success');
    } catch (err) {
      setError((err as Error).message);
    }
  };

  const open = (documentId: string): void => {
    if (!candidate) {
      return;
    }
    const secure = documentService.createSecureUrl(candidate.id, documentId);
    window.open(secure.url, '_blank', 'noopener,noreferrer');
  };

  const markPrimary = async (documentId: string): Promise<void> => {
    if (!candidate) {
      return;
    }
    try {
      await documentService.setPrimary(candidate.id, documentId);
      toastService.show('CV principal actualizado.', 'success');
    } catch (err) {
      toastService.show(
        err instanceof Error ? err.message : 'No se pudo actualizar el CV.',
        'error',
      );
    }
  };

  const remove = async (documentId: string): Promise<void> => {
    if (!candidate) {
      return;
    }
    const confirmed = await confirmDialogService.confirm({
      title: 'Eliminar documento',
      message: 'Se eliminará el documento seleccionado del candidato.',
      confirmText: 'Eliminar documento',
      cancelText: 'Cancelar',
      danger: true,
    });
    if (!confirmed) {
      return;
    }
    try {
      await documentService.remove(candidate.id, documentId);
      toastService.show('Documento eliminado.', 'success');
    } catch (err) {
      toastService.show(
        err instanceof Error ? err.message : 'No se pudo eliminar el documento.',
        'error',
      );
    }
  };

  const documents = candidate?.documents ?? [];

  return (
    <section className="section-block" data-testid="candidate-documents">
      <h3 className="section-title">Documentos</h3>
      {!documents.length ? <p className="empty-state">Sin CV adjunto.</p> : null}
      <div className="item-list">
        {documents.map((document) => (
          <div className="item-row" key={document.id}>
            <p className="item-main">
              <strong>{document.originalFilename}</strong>
              <span className="badge">
                {document.isPrimary ? 'Principal' : document.documentType}
              </span>
            </p>
            <button
              className="button secondary"
              type="button"
              disabled={!canDownload}
              onClick={() => open(document.id)}
            >
              Abrir seguro
            </button>
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
              <button className="button danger" type="button" onClick={() => remove(document.id)}>
                Eliminar
              </button>
            ) : null}
          </div>
        ))}
      </div>
      {canUpload ? (
        <form className="section-block" onSubmit={upload} noValidate>
          <div className="grid two">
            <div className="field">
              <label htmlFor="file">Archivo CV (PDF)</label>
              <input
                id="file"
                ref={fileInput}
                name="file"
                type="file"
                accept="application/pdf"
                onChange={onFileSelected}
              />
            </div>
            <div className="field">
              <label className="inline-check">
                <input
                  name="isPrimary"
                  type="checkbox"
                  checked={isPrimary}
                  onChange={(e) => setIsPrimary(e.target.checked)}
                />
                Marcar como CV principal
              </label>
            </div>
          </div>
          {error ? <p className="empty-state">{error}</p> : null}
          <div className="form-actions">
            <button className="button" type="submit" disabled={!selectedFile}>
              Subir CV
            </button>
          </div>
        </form>
      ) : null}
    </section>
  );
}
