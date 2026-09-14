import { useEffect, useMemo, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Link } from 'react-router';
import { useSearchPresets, useServices } from '../../../core/di/services-context';
import { formatDate } from '../../../core/i18n/format';
import { Pagination } from '../../../shared/components/pagination';
import { SearchCriteriaDialog } from '../../search/components/search-criteria-dialog';
import type { SearchPreset } from '../../search/models/search.models';
import {
  DEFAULT_PRESET_SORT,
  filterPresets,
  nextPresetSort,
  paginate,
  PRESETS_ROUTE,
  sortPresets,
  totalPages,
  type PresetSort,
  type PresetSortField,
} from './preset-list.logic';
import { useDeletePreset } from './use-delete-preset';
import './preset-list-page.css';

const SHORT_DATE_TIME: Intl.DateTimeFormatOptions = { dateStyle: 'short', timeStyle: 'short' };
const DATE_TIME: Intl.DateTimeFormatOptions = { dateStyle: 'medium', timeStyle: 'short' };

/** Decorative: the button carries the accessible name. */
function EyeIcon() {
  return (
    <svg viewBox="0 0 24 24" width="18" height="18" aria-hidden="true">
      <path
        d="M1.5 12S5.5 4.5 12 4.5 22.5 12 22.5 12 18.5 19.5 12 19.5 1.5 12 1.5 12Z"
        fill="none"
        stroke="currentColor"
        strokeWidth="1.8"
        strokeLinejoin="round"
      />
      <circle cx="12" cy="12" r="3.2" fill="none" stroke="currentColor" strokeWidth="1.8" />
    </svg>
  );
}

export function PresetListPage() {
  const { searchPresetsService } = useServices();
  const { t } = useTranslation();
  const { status, presets } = useSearchPresets();
  const deletePreset = useDeletePreset();

  const [text, setText] = useState('');
  const [sort, setSort] = useState<PresetSort>(DEFAULT_PRESET_SORT);
  const [page, setPage] = useState(1);
  const [pageSize, setPageSize] = useState(10);
  const [viewing, setViewing] = useState<SearchPreset | null>(null);

  useEffect(() => {
    // The failure is shown from the service state; a rejected promise here has nothing to add.
    void searchPresetsService.load().catch(() => undefined);
  }, [searchPresetsService]);

  const visible = useMemo(
    () => sortPresets(filterPresets(presets, text), sort),
    [presets, text, sort],
  );
  const pageCount = totalPages(visible.length, pageSize);
  const paged = useMemo(() => paginate(visible, page, pageSize), [visible, page, pageSize]);

  const isLoading = (status === 'idle' || status === 'loading') && !presets.length;

  const sortHeader = (field: PresetSortField, label: string) => (
    <th
      scope="col"
      aria-sort={
        sort.field === field ? (sort.direction === 'asc' ? 'ascending' : 'descending') : 'none'
      }
    >
      <button
        className="button ghost small preset-sort"
        type="button"
        data-testid={`preset-sort-${field}`}
        onClick={() => {
          setSort((current) => nextPresetSort(current, field));
          setPage(1);
        }}
      >
        {label}
      </button>
    </th>
  );

  return (
    <section className="page preset-list">
      <div className="toolbar">
        <div className="page-header">
          <h1>{t('presets.list.title')}</h1>
          <p className="muted">{t('presets.list.subtitle')}</p>
        </div>
        <Link className="button" to={`${PRESETS_ROUTE}/new`} data-testid="preset-new">
          {t('presets.list.new')}
        </Link>
      </div>

      <div className="panel">
        <div className="field">
          <label htmlFor="presetFilter">{t('presets.list.filter')}</label>
          <input
            id="presetFilter"
            name="presetFilter"
            value={text}
            onChange={(event) => {
              setText(event.target.value);
              setPage(1);
            }}
          />
        </div>
      </div>

      {isLoading ? (
        <p className="empty-state">{t('presets.list.loading')}</p>
      ) : status === 'failed' && !presets.length ? (
        <p className="empty-state" data-testid="presets-list-error">
          {t('presets.list.error')}
        </p>
      ) : !presets.length ? (
        <p className="empty-state" data-testid="presets-list-empty">
          {t('presets.list.empty')}
        </p>
      ) : !visible.length ? (
        <p className="empty-state">{t('presets.list.noMatches')}</p>
      ) : (
        <>
          <p className="muted" data-testid="presets-count">
            {t('presets.list.count', { shown: paged.length, total: visible.length })}
          </p>
          <div className="table-wrap">
            <table data-testid="presets-table">
              <thead>
                <tr>
                  {sortHeader('name', t('presets.column.name'))}
                  {sortHeader('updatedAt', t('presets.column.updatedAt'))}
                  {sortHeader('lastUsedAt', t('presets.column.lastUsedAt'))}
                  <th scope="col">{t('presets.column.actions')}</th>
                </tr>
              </thead>
              <tbody>
                {paged.map((preset) => {
                  const viewLabel = t('presets.action.view', { name: preset.name });
                  return (
                    <tr key={preset.id} data-testid="preset-row">
                      <td>
                        <div className="preset-name-cell">
                          <span data-testid="preset-row-name">{preset.name}</span>
                          <button
                            className="button ghost small icon-button"
                            type="button"
                            data-testid="preset-view"
                            aria-label={viewLabel}
                            title={viewLabel}
                            onClick={() => setViewing(preset)}
                          >
                            <EyeIcon />
                          </button>
                        </div>
                      </td>
                      <td>{formatDate(preset.updatedAt, SHORT_DATE_TIME)}</td>
                      <td>
                        {preset.lastUsedAt
                          ? formatDate(preset.lastUsedAt, SHORT_DATE_TIME)
                          : t('presets.lastUsed.never')}
                      </td>
                      <td>
                        <div className="preset-row-actions">
                          <Link
                            className="button secondary small"
                            to={`${PRESETS_ROUTE}/${preset.id}/edit`}
                            data-testid="preset-edit"
                          >
                            {t('presets.action.edit')}
                          </Link>
                          <button
                            className="button danger small"
                            type="button"
                            data-testid="preset-delete"
                            onClick={() => void deletePreset(preset)}
                          >
                            {t('presets.action.delete')}
                          </button>
                        </div>
                      </td>
                    </tr>
                  );
                })}
              </tbody>
            </table>
          </div>
          <Pagination
            page={Math.min(page, pageCount)}
            pageCount={pageCount}
            pageSize={pageSize}
            onPageChange={(next) => setPage(Math.min(Math.max(next, 1), pageCount))}
            onPageSizeChange={(next) => {
              setPageSize(next);
              setPage(1);
            }}
          />
        </>
      )}

      {viewing ? (
        <SearchCriteriaDialog
          title={viewing.name}
          filters={viewing.filters}
          onClose={() => setViewing(null)}
          details={
            <dl className="preset-meta">
              <dt>{t('presets.view.createdAt')}</dt>
              <dd>{formatDate(viewing.createdAt, DATE_TIME)}</dd>
              <dt>{t('presets.view.updatedAt')}</dt>
              <dd>{formatDate(viewing.updatedAt, DATE_TIME)}</dd>
              <dt>{t('presets.view.lastUsedAt')}</dt>
              <dd data-testid="preset-view-last-used">
                {viewing.lastUsedAt
                  ? formatDate(viewing.lastUsedAt, DATE_TIME)
                  : t('presets.lastUsed.never')}
              </dd>
            </dl>
          }
          actions={
            <Link
              className="button secondary"
              to={`${PRESETS_ROUTE}/${viewing.id}/edit`}
              data-testid="preset-view-edit"
            >
              {t('presets.action.edit')}
            </Link>
          }
        />
      ) : null}
    </section>
  );
}
