import { Link } from 'react-router';
import { usePermission, useServices } from '../../../core/di/services-context';
import { useErrorToast } from '../../../core/services/use-error-toast';
import type { SearchResult, SearchResultPage } from '../models/search.models';

interface SearchResultsProps {
  results: SearchResultPage;
  loading: boolean;
  failed: boolean;
  lastPage: number;
  onPageChange: (page: number) => void;
}

export function SearchResults({
  results,
  loading,
  failed,
  lastPage,
  onPageChange,
}: SearchResultsProps) {
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
            {results.items.length ? (
              results.items.map((result) => (
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
                <td colSpan={6} className="muted" data-testid="search-empty">
                  {loading
                    ? 'Buscando…'
                    : failed
                      ? 'No se pudo completar la búsqueda.'
                      : 'Sin resultados.'}
                </td>
              </tr>
            )}
          </tbody>
        </table>
      </div>
      {/*
        The count comes from the server, never from `results.items.length`: these items are
        one page, and reporting their number as the total is exactly the mistake paging
        introduces.
      */}
      <div className="toolbar" data-testid="search-pagination">
        <p className="muted" data-testid="search-total">
          {results.totalCount === 1
            ? '1 candidato encontrado'
            : `${results.totalCount} candidatos encontrados`}
          {results.totalCount > 0 ? ` · Página ${results.page} de ${lastPage}` : ''}
        </p>
        <div className="form-actions">
          <button
            className="button ghost small"
            type="button"
            name="previousPage"
            data-testid="search-previous-page"
            disabled={loading || results.page <= 1}
            onClick={() => onPageChange(results.page - 1)}
          >
            Anterior
          </button>
          <button
            className="button ghost small"
            type="button"
            name="nextPage"
            data-testid="search-next-page"
            disabled={loading || results.page >= lastPage}
            onClick={() => onPageChange(results.page + 1)}
          >
            Siguiente
          </button>
        </div>
      </div>
      {!canDownload ? (
        <p className="empty-state">Tu rol no permite abrir CVs desde resultados.</p>
      ) : null}
    </div>
  );
}
