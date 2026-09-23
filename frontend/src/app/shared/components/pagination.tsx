import { useTranslation } from 'react-i18next';

interface Props {
  page: number;
  pageCount: number;
  pageSize: number;
  /** The sizes on offer. A server-paged screen passes the ones its contract allows. */
  pageSizes?: number[];
  onPageChange: (page: number) => void;
  onPageSizeChange: (pageSize: number) => void;
}

const DEFAULT_PAGE_SIZES = [10, 20, 50];

export function Pagination({
  page,
  pageCount,
  pageSize,
  pageSizes = DEFAULT_PAGE_SIZES,
  onPageChange,
  onPageSizeChange,
}: Props) {
  const { t } = useTranslation();
  // A page past the end (for example after rows were removed elsewhere) still needs a way
  // back, so the pager shows whenever there is anywhere to go.
  const showPager = pageCount > 1 || page > 1;
  return (
    <div className="toolbar results-footer">
      <div className="page-size">
        <label htmlFor="page-size">{t('pagination.pageSize')}</label>
        <select
          id="page-size"
          name="pageSize"
          value={pageSize}
          onChange={(event) => onPageSizeChange(Number(event.target.value))}
        >
          {pageSizes.map((size) => (
            <option key={size} value={size}>
              {size}
            </option>
          ))}
        </select>
      </div>
      {showPager ? (
        <div className="pager">
          <button
            className="button secondary"
            type="button"
            name="previousPage"
            disabled={page <= 1}
            onClick={() => onPageChange(page - 1)}
          >
            {t('pagination.previous')}
          </button>
          <span className="muted" data-testid="pagination-status">
            {t('pagination.status', { page, pageCount })}
          </span>
          <button
            className="button secondary"
            type="button"
            name="nextPage"
            disabled={page >= pageCount}
            onClick={() => onPageChange(page + 1)}
          >
            {t('pagination.next')}
          </button>
        </div>
      ) : null}
    </div>
  );
}
