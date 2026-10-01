import { Fragment, type ReactNode } from 'react';
import { useTranslation } from 'react-i18next';
import { Link } from 'react-router';
import { formatDate } from '../../../core/i18n/format';
import '../../../shared/components/data-table.css';
import { useRowLink } from '../../../shared/components/row-link';
import {
  RowCvActions,
  RowCvInlineRow,
} from '../../candidates/components/row-cv-preview/row-cv-preview';
import { useRowCvTable } from '../../candidates/components/row-cv-preview/row-cv-preview.context';
import { displayPhone, mailtoHref } from '../../candidates/contact-links';
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
  /**
   * This table's key in the page's row CV preview (KTL-35), so the position page can tell its
   * matches from its linked candidates.
   */
  tableId?: string;
}

export function SearchResults({
  results,
  loading,
  failed,
  lastPage,
  onPageChange,
  renderRowAction,
  tableId = 'search-results',
}: SearchResultsProps) {
  const { t } = useTranslation();
  const rowLink = useRowLink();
  const cv = useRowCvTable(
    tableId,
    results.items.map((result) => result.candidateId),
  );
  const columnCount = 4 + (renderRowAction ? 1 : 0) + (cv.enabled ? 1 : 0);

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
              <th>{t('search.results.column.updated')}</th>
              {renderRowAction ? <th></th> : null}
              {cv.enabled ? <th>{t('search.results.column.cv')}</th> : null}
            </tr>
          </thead>
          <tbody>
            {results.items.length ? (
              results.items.map((result) => (
                <Fragment key={result.candidateId}>
                  {/* The whole row opens the candidate; the name is its keyboard link. */}
                  <tr
                    ref={cv.rowRef(result.candidateId)}
                    className={
                      cv.isOpen(result.candidateId) ? 'row-link-row is-cv-open' : 'row-link-row'
                    }
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
                    <td>{displayPhone(result.phone)}</td>
                    <td>
                      <span className="badge">{result.status}</span>
                    </td>
                    <td>
                      {formatDate(result.updatedAt, {
                        day: '2-digit',
                        month: '2-digit',
                        year: 'numeric',
                      })}
                    </td>
                    {renderRowAction ? (
                      <td data-row-link-ignore="">
                        <div className="form-actions">{renderRowAction(result)}</div>
                      </td>
                    ) : null}
                    {cv.enabled ? (
                      <td data-row-link-ignore="">
                        <RowCvActions
                          tableId={tableId}
                          candidateId={result.candidateId}
                          name={`${result.firstName} ${result.lastName}`}
                          downloadable={result.primaryCvDownloadable}
                          previewable={result.primaryCvPreviewable}
                        />
                      </td>
                    ) : null}
                  </tr>
                  <RowCvInlineRow
                    tableId={tableId}
                    candidateId={result.candidateId}
                    colSpan={columnCount}
                  />
                </Fragment>
              ))
            ) : (
              <tr>
                <td colSpan={columnCount} className="muted" data-testid="search-empty">
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
    </div>
  );
}
