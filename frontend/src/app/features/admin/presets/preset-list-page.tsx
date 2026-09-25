import { useEffect, useMemo, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Link } from 'react-router';
import { useSearchPresets, useServices } from '../../../core/di/services-context';
import { formatDate } from '../../../core/i18n/format';
import '../../../shared/components/data-table.css';
import { Pagination } from '../../../shared/components/pagination';
import { useRowLink } from '../../../shared/components/row-link';
import { SearchCriteriaSummary } from '../../search/components/search-criteria-summary';
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

export function PresetListPage() {
  const { searchPresetsService } = useServices();
  const { t } = useTranslation();
  const { status, presets } = useSearchPresets();
  const deletePreset = useDeletePreset();
  const rowLink = useRowLink();

  const [text, setText] = useState('');
  const [sort, setSort] = useState<PresetSort>(DEFAULT_PRESET_SORT);
  const [page, setPage] = useState(1);
  const [pageSize, setPageSize] = useState(10);

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
          <div className="panel table-wrap">
            <table className="data-table" data-testid="presets-table">
              <thead>
                <tr>
                  {sortHeader('name', t('presets.column.name'))}
                  {sortHeader('updatedAt', t('presets.column.updatedAt'))}
                  {sortHeader('lastUsedAt', t('presets.column.lastUsedAt'))}
                  <th scope="col">{t('presets.column.actions')}</th>
                </tr>
              </thead>
              {paged.map((preset) => {
                const editPath = `${PRESETS_ROUTE}/${preset.id}/edit`;
                return (
                  // One group per preset: its values line and its criteria line open the edit
                  // page as a single record; the name is the keyboard link.
                  <tbody
                    key={preset.id}
                    className="row-link-group"
                    data-testid="preset-row"
                    onClick={rowLink(editPath)}
                    onAuxClick={rowLink(editPath)}
                  >
                    <tr>
                      <td>
                        <Link className="row-link" to={editPath} data-testid="preset-row-name">
                          {preset.name}
                        </Link>
                      </td>
                      <td>{formatDate(preset.updatedAt, SHORT_DATE_TIME)}</td>
                      <td>
                        {preset.lastUsedAt
                          ? formatDate(preset.lastUsedAt, SHORT_DATE_TIME)
                          : t('presets.lastUsed.never')}
                      </td>
                      <td>
                        <div className="preset-row-actions">
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
                    <tr>
                      <td colSpan={4} className="preset-criteria" data-testid="preset-criteria">
                        <SearchCriteriaSummary filters={preset.filters} layout="inline" />
                      </td>
                    </tr>
                  </tbody>
                );
              })}
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
    </section>
  );
}
