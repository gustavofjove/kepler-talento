import { useCallback, useEffect, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Link } from 'react-router';
import { usePermission, useServices } from '../../core/di/services-context';
import { formatDate, formatNumber } from '../../core/i18n/format';
import { useErrorToast } from '../../core/services/use-error-toast';
import '../../shared/components/data-table.css';
import { Pagination } from '../../shared/components/pagination';
import { useRowLink } from '../../shared/components/row-link';
import { usePositions } from './use-positions';
import type { PositionListQuery, PositionStatus } from './position.models';
import './positions.css';

export function PositionListPage() {
  const { t } = useTranslation();
  const { positionService } = useServices();
  const page = usePositions();
  const canManage = usePermission('positions.manage');
  const notifyError = useErrorToast();
  const rowLink = useRowLink();
  const [status, setStatus] = useState<PositionStatus | 'all'>('open');
  const [text, setText] = useState('');
  const [sortField, setSortField] = useState<PositionListQuery['sortField']>('updatedAt');
  const [loading, setLoading] = useState(true);
  const load = useCallback(
    (nextPage = 1, pageSize = page.pageSize) => {
      setLoading(true);
      void positionService
        .list({
          status,
          text,
          sortField,
          sortDirection: sortField === 'updatedAt' ? 'desc' : 'asc',
          page: nextPage,
          pageSize,
        })
        .catch((error) => notifyError(error, t('positions.list.error')))
        .finally(() => setLoading(false));
    },
    [notifyError, page.pageSize, positionService, sortField, status, t, text],
  );
  useEffect(() => {
    const timeout = window.setTimeout(() => load(1), 250);
    return () => window.clearTimeout(timeout);
  }, [load]);
  return (
    <section className="page positions-page">
      <div className="toolbar">
        <div className="page-header">
          <h1>{t('positions.list.title')}</h1>
        </div>
        {canManage ? (
          <Link className="button primary" to="/app/positions/new" data-testid="position-create">
            {t('positions.actions.create')}
          </Link>
        ) : null}
      </div>
      <div className="panel position-filters">
        <div className="field">
          <label htmlFor="position-text-filter">{t('positions.filters.text')}</label>
          <input
            id="position-text-filter"
            name="positionText"
            data-testid="position-text-filter"
            value={text}
            onChange={(event) => setText(event.target.value)}
          />
        </div>
        <div className="field">
          <label htmlFor="position-status-filter">{t('positions.filters.status')}</label>
          <select
            id="position-status-filter"
            name="positionStatus"
            value={status}
            onChange={(event) => setStatus(event.target.value as PositionStatus | 'all')}
          >
            <option value="open">{t('positions.status.open')}</option>
            <option value="closed">{t('positions.status.closed')}</option>
            <option value="all">{t('positions.status.all')}</option>
          </select>
        </div>
        <div className="field">
          <label htmlFor="position-sort-filter">{t('positions.filters.sort')}</label>
          <select
            id="position-sort-filter"
            name="positionSort"
            value={sortField}
            onChange={(event) => setSortField(event.target.value as PositionListQuery['sortField'])}
          >
            <option value="updatedAt">{t('positions.sort.updatedAt')}</option>
            <option value="title">{t('positions.sort.title')}</option>
            <option value="location">{t('positions.sort.location')}</option>
            <option value="status">{t('positions.sort.status')}</option>
          </select>
        </div>
      </div>
      {loading ? (
        <p role="status">{t('positions.list.loading')}</p>
      ) : page.items.length === 0 ? (
        <p>{t('positions.list.empty')}</p>
      ) : (
        <div className="panel table-wrap positions-table">
          <table className="data-table">
            <thead>
              <tr>
                <th>{t('positions.form.title')}</th>
                <th>{t('positions.form.location')}</th>
                <th>{t('positions.form.status')}</th>
                <th>{t('positions.list.candidates')}</th>
                <th>{t('positions.list.updated')}</th>
              </tr>
            </thead>
            <tbody>
              {page.items.map((item) => (
                // The whole row opens the position; the title is its keyboard link.
                <tr
                  key={item.id}
                  className="row-link-row"
                  data-testid="position-row"
                  onClick={rowLink(`/app/positions/${item.id}`)}
                  onAuxClick={rowLink(`/app/positions/${item.id}`)}
                >
                  <td>
                    <Link className="row-link" to={`/app/positions/${item.id}`}>
                      {item.title}
                    </Link>
                  </td>
                  <td>{item.location || '—'}</td>
                  <td>
                    <span className="badge">{t(`positions.status.${item.status}`)}</span>
                  </td>
                  <td data-testid="position-candidate-count">
                    {formatNumber(item.candidateCount)}
                  </td>
                  <td>{formatDate(item.updatedAtUtc, { dateStyle: 'medium' })}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
      <Pagination
        page={page.page}
        pageSize={page.pageSize}
        pageCount={Math.max(1, Math.ceil(page.totalCount / page.pageSize))}
        pageSizes={[25, 50, 100]}
        onPageChange={(next) => load(next)}
        onPageSizeChange={(size) => load(1, size)}
      />
    </section>
  );
}
