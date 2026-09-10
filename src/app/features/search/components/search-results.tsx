import { Link } from 'react-router';
import { usePermission, useServices } from '../../../core/di/services-context';
import { useErrorToast } from '../../../core/services/use-error-toast';
import type { SearchResult } from '../models/search.models';

export function SearchResults({ results }: { results: SearchResult[] }) {
  const { documentService, toastService } = useServices();
  const notifyError = useErrorToast();
  const canDownload = usePermission('download_candidate_documents');

  const canOpenCv = (result: SearchResult): boolean =>
    canDownload && Boolean(result.hasPrimaryCv && result.primaryCvDocumentId);

  const openCv = async (result: SearchResult): Promise<void> => {
    if (!canOpenCv(result)) {
      toastService.show('No hay CV principal disponible o no tienes permiso.', 'warning');
      return;
    }
    try {
      await documentService.download(result.candidateId, result.primaryCvDocumentId!, 'cv.pdf');
    } catch (error) {
      notifyError(error, 'No se pudo descargar el CV.');
    }
  };

  return (
    <div className="section-block">
      <div className="table-wrap">
        <table>
          <thead>
            <tr>
              <th>Candidato</th>
              <th>Teléfono</th>
              <th>Estado</th>
              <th>CV</th>
              <th>Actualizado</th>
              <th></th>
            </tr>
          </thead>
          <tbody>
            {results.length ? (
              results.map((result) => (
                <tr key={result.candidateId}>
                  <td>
                    <strong>
                      {result.firstName} {result.lastName}
                    </strong>
                    <div className="muted">{result.email}</div>
                  </td>
                  <td>{result.phone}</td>
                  <td>
                    <span className="badge">{result.status}</span>
                  </td>
                  <td>{result.hasPrimaryCv ? 'Disponible' : 'Pendiente'}</td>
                  <td>{result.updatedAt.slice(0, 10)}</td>
                  <td>
                    <div className="form-actions">
                      <Link
                        className="button secondary"
                        to={`/app/candidates/${result.candidateId}`}
                      >
                        Detalle
                      </Link>
                      <button
                        className="button ghost"
                        type="button"
                        disabled={!canOpenCv(result)}
                        onClick={() => void openCv(result)}
                      >
                        Abrir CV
                      </button>
                    </div>
                  </td>
                </tr>
              ))
            ) : (
              <tr>
                <td colSpan={6} className="muted">
                  Sin resultados.
                </td>
              </tr>
            )}
          </tbody>
        </table>
      </div>
      {!canDownload ? (
        <p className="empty-state">Tu rol no permite abrir CVs desde resultados.</p>
      ) : null}
    </div>
  );
}
