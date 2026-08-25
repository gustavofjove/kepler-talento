interface Props {
  page: number;
  pageCount: number;
  pageSize: number;
  onPageChange: (page: number) => void;
  onPageSizeChange: (pageSize: number) => void;
}

const PAGE_SIZES = [10, 20, 50];

export function Pagination({ page, pageCount, pageSize, onPageChange, onPageSizeChange }: Props) {
  return (
    <div className="toolbar results-footer">
      <div className="page-size">
        <label htmlFor="page-size">Registros por página</label>
        <select
          id="page-size"
          name="pageSize"
          value={pageSize}
          onChange={(event) => onPageSizeChange(Number(event.target.value))}
        >
          {PAGE_SIZES.map((size) => (
            <option key={size} value={size}>
              {size}
            </option>
          ))}
        </select>
      </div>
      {pageCount > 1 ? (
        <div className="pager">
          <button
            className="button secondary"
            type="button"
            disabled={page <= 1}
            onClick={() => onPageChange(page - 1)}
          >
            Anterior
          </button>
          <span className="muted">
            Página {page} de {pageCount}
          </span>
          <button
            className="button secondary"
            type="button"
            disabled={page >= pageCount}
            onClick={() => onPageChange(page + 1)}
          >
            Siguiente
          </button>
        </div>
      ) : null}
    </div>
  );
}
