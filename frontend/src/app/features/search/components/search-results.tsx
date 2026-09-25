import type { ReactNode } from 'react';
import { useTranslation } from 'react-i18next';
import { Link } from 'react-router';
import { usePermission, useServices } from '../../../core/di/services-context';
import { formatDate } from '../../../core/i18n/format';
import { useErrorToast } from '../../../core/services/use-error-toast';
import '../../../shared/components/data-table.css';
import { useRowLink } from '../../../shared/components/row-link';
import { mailtoHref } from '../../candidates/contact-links';
import type { SearchResult, SearchResultPage } from '../models/search.models';

interface SearchResultsProps {
  results: SearchResultPage;
  loading: boolean;
  failed: boolean;
  lastPage: number;
  onPageChange: (page: number) => void;
  /**
   * An extra action per row, rendered first in the actions cell. The position page uses it for «Añadir a
   * la posición» (KTL-30); the advanced search page passes nothing and renders as before.
   */
  renderRowAction?: (result: SearchResult) => ReactNode;
  /** «Abrir CV» per row; the position page hides it. Defaults to shown. */
  showOpenCv?: boolean;
}

export function SearchResults({
  results,
  loading,
  failed,
  lastPage,
  onPageChange,
  renderRowAction,
  showOpenCv = true,
}: SearchResultsProps) {
  const { t } = useTranslation();
  const { documentService, toastService } = useServices();
  const notifyError = useErrorToast();
  const canDownload = usePermission('documents.download');
  const rowLink = useRowLink();

  const canOpenCv = (result: SearchResult): boolean =>
    canDownload && Boolean(result.hasPrimaryCv && result.primaryCvDocumentId);

  const openCv = async (result: SearchResult): Promise<void> => {
    if (!canOpenCv(result)) {
      toastService.show(t('search.results.cvUnavailable'), 'warning');
      return;
    }
    try {
      await documentService.download(result.candidateId, result.primaryCvDocumentId!, 'cv.pdf');
    } catch (error) {
      notifyError(error, t('search.results.cvDownloadFailed'));
    }
  };

  const total = (): string => {
    const one = results.totalCount === 1;
    if (results.totalCount === 0) return t('search.results.countMany', { count: 0 });
    return t(one ? 'search.results.countOnePaged' : 'search.results.countManyPaged', {
      count: results.totalCount,
      page: results.page,
      lastPage,
    });
  };

  return (
    <div className="section-block">
      <div className="table-wrap">
        <table className="data-table">
          <thead>
            <tr>
              <th>{t('search.results.column.candidate')}</th>
              <th>{t('search.results.column.phone')}</th>
              <th>{t('search.results.column.status')}</th>
              <th>{t('search.results.column.cv')}</th>
              <th>{t('search.results.column.updated')}</th>
              <th></th>
            </tr>
          </thead>
          <tbody>
            {results.items.length ? (
              results.items.map((result) => (
                // The whole row opens the candidate; the name is its keyboard link.
                <tr
                  key={result.candidateId}
                  className="row-link-row"
                  onClick={rowLink(`/app/candidates/${result.candidateId}`)}
                  onAuxClick={rowLink(`/app/candidates/${result.candidateId}`)}
                >
                  <td>
                    <Link className="row-link" to={`/app/candidates/${result.candidateId}`}>
                      {result.firstName} {result.lastName}
                    </Link>
                    {result.email ? (
                      <div className="muted contact-links">
                        <a href={mailtoHref(result.email)}>{result.email}</a>
                      </div>
                    ) : null}
                  </td>
                  <td>{result.phone}</td>
                  <td>
                    <span className="badge">{result.status}</span>
                  </td>
                  <td>
                    {t(
                      result.hasPrimaryCv
                        ? 'search.results.cvAvailable'
                        : 'search.results.cvPending',
                    )}
                  </td>
                  <td>
                    {formatDate(result.updatedAt, {
                      day: '2-digit',
                      month: '2-digit',
                      year: 'numeric',
                    })}
                  </td>
                  <td>
                    <div className="form-actions">
                      {renderRowAction?.(result)}
                      {showOpenCv ? (
                        <button
                          className="button ghost"
                          type="button"
                          disabled={!canOpenCv(result)}
                          onClick={() => void openCv(result)}
                        >
                          {t('search.results.openCv')}
                        </button>
                      ) : null}
                    </div>
                  </td>
                </tr>
              ))
            ) : (
              <tr>
                <td colSpan={6} className="muted" data-testid="search-empty">
                  {loading
                    ? t('search.results.searching')
                    : failed
                      ? t('search.results.failed')
                      : t('search.results.empty')}
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
          {total()}
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
            {t('search.results.previous')}
          </button>
          <button
            className="button ghost small"
            type="button"
            name="nextPage"
            data-testid="search-next-page"
            disabled={loading || results.page >= lastPage}
            onClick={() => onPageChange(results.page + 1)}
          >
            {t('search.results.next')}
          </button>
        </div>
      </div>
      {showOpenCv && !canDownload ? (
        <p className="empty-state">{t('search.results.cvNotAllowed')}</p>
      ) : null}
    </div>
  );
}
