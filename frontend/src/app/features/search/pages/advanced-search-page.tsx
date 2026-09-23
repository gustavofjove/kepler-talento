import { useCallback, useEffect, useRef, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Link } from 'react-router';
import { usePermission, useSearchPresets, useServices } from '../../../core/di/services-context';
import { useErrorToast } from '../../../core/services/use-error-toast';
import { AppError, toAppError } from '../../../shared/models/error.models';
import type { ExportBatchRecord } from '../services/export.service';
import { SearchCriteriaForm } from '../components/search-criteria-form';
import { SearchResults } from '../components/search-results';
import {
  DEFAULT_SEARCH_PAGE_SIZE,
  cloneSearchFilters,
  type SearchFilters,
  type SearchResultPage,
} from '../models/search.models';
import '../../../shared/components/modal.css';
import './advanced-search-page.css';

/**
 * How long the page waits before asking the server.
 *
 * Long enough that adjusting a filter does not issue a request per keystroke, short enough
 * that a deliberate search still feels immediate.
 */
const SEARCH_DEBOUNCE_MS = 300;

const EMPTY_PAGE: SearchResultPage = {
  items: [],
  page: 1,
  pageSize: DEFAULT_SEARCH_PAGE_SIZE,
  totalCount: 0,
};

export function AdvancedSearchPage() {
  const {
    candidateSearchService,
    searchPresetsService,
    exportService,
    toastService,
    observabilityService,
  } = useServices();
  const { t } = useTranslation();
  const notifyError = useErrorToast();

  // filters and results are deliberately separate. Editing a filter must NOT
  // re-run the search - results only change in run/clear/loadPreset/page, exactly
  // as the Angular version behaved (FR-4).
  const [filters, setFilters] = useState<SearchFilters>(() =>
    searchPresetsService.loadLastFilters(),
  );
  // One page, never a complete collection: the totals come from the server, and nothing
  // here treats `results.items.length` as "how many candidates matched".
  const [results, setResults] = useState<SearchResultPage>(EMPTY_PAGE);
  const [loading, setLoading] = useState(true);
  const [failed, setFailed] = useState(false);
  const presets = useSearchPresets();
  const [exportBatches, setExportBatches] = useState<ExportBatchRecord[]>(() =>
    exportService.listBatches(),
  );
  const [selectedPresetId, setSelectedPresetId] = useState('');
  const [showExportHistory, setShowExportHistory] = useState(false);
  // The filter panel starts open, and collapses when the user runs a search.
  const [filtersCollapsed, setFiltersCollapsed] = useState(false);

  // The in-flight request. Aborting the previous controller before starting another is what
  // makes a superseded search stop at the network instead of arriving late and overwriting
  // the newer one's results.
  const inFlight = useRef<AbortController | null>(null);
  const debounce = useRef<ReturnType<typeof setTimeout> | null>(null);

  const execute = useCallback(
    async (next: SearchFilters, page: number): Promise<void> => {
      inFlight.current?.abort();
      const controller = new AbortController();
      inFlight.current = controller;
      setLoading(true);
      try {
        const found = await candidateSearchService.search(next, {
          page,
          pageSize: DEFAULT_SEARCH_PAGE_SIZE,
          signal: controller.signal,
        });
        // A response that arrived after this request was superseded belongs to nobody.
        if (controller.signal.aborted) {
          return;
        }
        setResults(found);
        setFailed(false);
        setLoading(false);
      } catch (error) {
        // A cancelled request is not a failure the user needs to hear about: it was
        // cancelled because they asked for something newer.
        if (
          controller.signal.aborted ||
          (error instanceof AppError && error.code === 'CANCELLED')
        ) {
          return;
        }
        setFailed(true);
        setLoading(false);
        notifyError(error, t('search.page.searchFailed'));
      }
    },
    [candidateSearchService, notifyError, t],
  );

  /** Debounced entry point for user-driven searches. */
  const schedule = useCallback(
    (next: SearchFilters, page: number): void => {
      if (debounce.current) {
        clearTimeout(debounce.current);
      }
      // Aborted here as well as in `execute`, so a request superseded during the debounce
      // window stops immediately rather than waiting out the delay first.
      inFlight.current?.abort();
      setLoading(true);
      debounce.current = setTimeout(() => void execute(next, page), SEARCH_DEBOUNCE_MS);
    },
    [execute],
  );

  useEffect(() => {
    void execute(searchPresetsService.loadLastFilters(), 1);
    void searchPresetsService.load().catch(() => {
      toastService.show(t('search.presets.loadFailed'), 'error');
    });
    return () => {
      if (debounce.current) {
        clearTimeout(debounce.current);
      }
      inFlight.current?.abort();
    };
  }, [execute, searchPresetsService, toastService, t]);

  const run = (next: SearchFilters): void => {
    const cloned = cloneSearchFilters(next);
    setFilters(cloned);
    searchPresetsService.rememberLastFilters(cloned);
    setFiltersCollapsed(true);
    schedule(cloned, 1);
  };

  const clear = (): void => {
    const empty = candidateSearchService.emptyFilters();
    setFilters(empty);
    searchPresetsService.rememberLastFilters(empty);
    setFiltersCollapsed(false);
    schedule(empty, 1);
  };

  const goToPage = (page: number): void => {
    void execute(filters, page);
  };

  /**
   * Applying is the only thing this page does with presets. Creating, editing and deleting
   * them is the administration section's job (KTL-14).
   */
  const loadPreset = async (presetId: string): Promise<void> => {
    setSelectedPresetId(presetId);
    if (!presetId) {
      return;
    }
    try {
      const applied = await searchPresetsService.applyPreset(presetId);
      setFilters(applied);
      searchPresetsService.rememberLastFilters(applied);
      setFiltersCollapsed(true);
      await execute(applied, 1);
      toastService.show(t('search.presets.applied'), 'success');
    } catch (error) {
      // The current filters are left exactly as they were: a preset that could not be applied
      // - typically one an administrator has just deleted - must not half-replace them.
      setSelectedPresetId('');
      notifyError(error, t('search.presets.applyFailed'));
    }
  };

  const exportCsv = (): void => {
    if (!results.items.length) {
      toastService.show(t('search.export.noResults'), 'warning');
      return;
    }
    const requestId = observabilityService.log('export.started', { rows: results.items.length });
    try {
      // Exports the page on screen. It is not the whole result set, and the message says so
      // rather than letting the user believe they exported every match.
      const count = exportService.exportCandidatesToCsv(results.items);
      setExportBatches(exportService.listBatches());
      toastService.show(t('search.export.done', { count }), 'success');
      observabilityService.log('export.completed', { request_id: requestId, rows: count });
    } catch (error) {
      const appError = toAppError(error, 'VALIDATION_ERROR');
      toastService.show(appError.message, 'error');
      observabilityService.log('export.failed', {
        request_id: requestId,
        code: appError.code,
        message: appError.message,
      });
    }
  };

  const canExport = usePermission('candidates.export');
  const canManagePresets = usePermission('presets.manage');
  const lastPage = Math.max(1, Math.ceil(results.totalCount / results.pageSize));

  return (
    <section className="page">
      <div className="toolbar">
        <div className="page-header">
          <h1>{t('search.page.title')}</h1>
          <p className="muted">{t('search.page.subtitle')}</p>
        </div>
        <div className="toolbar">
          <button
            className="button secondary"
            type="button"
            disabled={!canExport}
            onClick={exportCsv}
          >
            {t('search.page.export')}
          </button>
          <button
            className="button ghost"
            type="button"
            data-testid="open-export-history"
            onClick={() => {
              setExportBatches(exportService.listBatches());
              setShowExportHistory(true);
            }}
          >
            {t('search.page.exportHistory')}
          </button>
        </div>
      </div>

      {!canExport ? <p className="empty-state">{t('search.page.exportNotAllowed')}</p> : null}

      <div className="panel grid three">
        <div className="field">
          <label htmlFor="selectedPreset">{t('search.presets.picker')}</label>
          <select
            id="selectedPreset"
            name="selectedPreset"
            value={selectedPresetId}
            disabled={presets.status === 'loading'}
            onChange={(e) => void loadPreset(e.target.value)}
          >
            <option value="">{t('search.presets.placeholder')}</option>
            {presets.presets.map((preset) => (
              <option key={preset.id} value={preset.id}>
                {preset.name}
              </option>
            ))}
          </select>
          {presets.status === 'failed' ? (
            <p className="muted" data-testid="presets-error">
              {t('search.presets.loadFailed')}
            </p>
          ) : null}
        </div>
        {canManagePresets ? (
          <div className="form-actions preset-actions">
            <Link
              className="button ghost small"
              to="/app/admin/presets"
              data-testid="manage-presets-link"
            >
              {t('search.presets.manage')}
            </Link>
          </div>
        ) : null}
      </div>

      <div className="panel">
        <SearchCriteriaForm
          filters={filters}
          onFiltersChange={setFilters}
          collapsed={filtersCollapsed}
          onCollapsedChange={setFiltersCollapsed}
          onSubmit={run}
          actions={
            <>
              <button className="button" type="submit">
                {t('search.actions.search')}
              </button>
              <button className="button secondary" type="button" onClick={clear}>
                {t('search.actions.clear')}
              </button>
            </>
          }
        />
      </div>
      <div className="panel">
        <SearchResults
          results={results}
          loading={loading}
          failed={failed}
          onPageChange={goToPage}
          lastPage={lastPage}
        />
      </div>

      {showExportHistory ? (
        <div className="overlay" onClick={() => setShowExportHistory(false)}>
          <section
            className="modal"
            role="dialog"
            aria-modal="true"
            aria-labelledby="export-history-title"
            data-testid="export-history-modal"
            onClick={(event) => event.stopPropagation()}
          >
            <h2 id="export-history-title">{t('search.page.exportHistory')}</h2>
            {exportBatches.length ? (
              <div className="table-wrap">
                <table>
                  <thead>
                    <tr>
                      <th>{t('search.export.history.date')}</th>
                      <th>{t('search.export.history.file')}</th>
                      <th>{t('search.export.history.rows')}</th>
                      <th>{t('search.export.history.status')}</th>
                    </tr>
                  </thead>
                  <tbody>
                    {exportBatches.map((batch) => (
                      <tr key={batch.id}>
                        <td>{batch.exportedAt.slice(0, 19)}</td>
                        <td>{batch.fileName}</td>
                        <td>{batch.rowCount}</td>
                        <td>
                          <span className="status status--success">
                            {t('search.export.history.completed')}
                          </span>
                        </td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            ) : (
              <p className="empty-state">{t('search.export.history.empty')}</p>
            )}
            <div className="modal-actions">
              <button
                className="button ghost"
                type="button"
                data-testid="close-export-history"
                onClick={() => setShowExportHistory(false)}
              >
                {t('search.export.history.close')}
              </button>
            </div>
          </section>
        </div>
      ) : null}
    </section>
  );
}
