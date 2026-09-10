import { useCallback, useEffect, useRef, useState } from 'react';
import { usePermission, useSearchPresets, useServices } from '../../../core/di/services-context';
import { useErrorToast } from '../../../core/services/use-error-toast';
import { AppError, toAppError } from '../../../shared/models/error.models';
import type { ExportBatchRecord } from '../services/export.service';
import { SearchFilters as SearchFiltersPanel } from '../components/search-filters';
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
    confirmDialogService,
  } = useServices();
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
  const [presetName, setPresetName] = useState('');
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
        notifyError(error, 'No se pudo completar la búsqueda.');
      }
    },
    [candidateSearchService, notifyError],
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
      toastService.show('No se pudieron cargar las búsquedas guardadas.', 'error');
    });
    return () => {
      if (debounce.current) {
        clearTimeout(debounce.current);
      }
      inFlight.current?.abort();
    };
  }, [execute, searchPresetsService, toastService]);

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

  const savePreset = async (): Promise<void> => {
    const name = presetName.trim();
    if (!name) {
      toastService.show('Indica un nombre para la búsqueda guardada.', 'warning');
      return;
    }
    try {
      // Saving over the selected preset renames and replaces it; saving with no selection
      // creates a new one. The server refuses a name this owner already uses, so an
      // accidental overwrite is a conflict rather than a silent replacement.
      const saved = selectedPresetId
        ? await searchPresetsService.updatePreset(selectedPresetId, name, filters)
        : await searchPresetsService.createPreset(name, filters);
      setSelectedPresetId(saved.id);
      setPresetName(saved.name);
      toastService.show(`Preset guardado: ${saved.name}.`, 'success');
    } catch (error) {
      notifyError(error, 'No se pudo guardar el preset.');
    }
  };

  const loadPreset = async (presetId: string): Promise<void> => {
    setSelectedPresetId(presetId);
    if (!presetId) {
      return;
    }
    try {
      const applied = await searchPresetsService.applyPreset(presetId);
      setFilters(applied);
      searchPresetsService.rememberLastFilters(applied);
      setPresetName(presets.presets.find((item) => item.id === presetId)?.name ?? '');
      setFiltersCollapsed(true);
      await execute(applied, 1);
      toastService.show('Preset aplicado correctamente.', 'success');
    } catch (error) {
      setSelectedPresetId('');
      notifyError(error, 'No se pudo cargar el preset.');
    }
  };

  const deletePreset = async (): Promise<void> => {
    if (!selectedPresetId) {
      toastService.show('Selecciona un preset para eliminar.', 'warning');
      return;
    }
    const preset = presets.presets.find((item) => item.id === selectedPresetId);
    const confirmDelete = await confirmDialogService.confirm({
      title: 'Eliminar preset guardado',
      message: `Se eliminará el preset "${preset?.name ?? 'sin nombre'}".`,
      confirmText: 'Eliminar',
      cancelText: 'Cancelar',
      danger: true,
    });
    if (!confirmDelete) {
      return;
    }
    try {
      await searchPresetsService.removePreset(selectedPresetId);
      setSelectedPresetId('');
      setPresetName('');
      toastService.show('Preset eliminado.', 'success');
    } catch (error) {
      notifyError(error, 'No se pudo eliminar el preset.');
    }
  };

  const exportCsv = (): void => {
    if (!results.items.length) {
      toastService.show('No hay resultados para exportar.', 'warning');
      return;
    }
    const requestId = observabilityService.log('export.started', { rows: results.items.length });
    try {
      // Exports the page on screen. It is not the whole result set, and the message says so
      // rather than letting the user believe they exported every match.
      const count = exportService.exportCandidatesToCsv(results.items);
      setExportBatches(exportService.listBatches());
      toastService.show(
        `Exportación generada (${count} filas de esta página) sin rutas internas de Storage.`,
        'success',
      );
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

  const canExport = usePermission('export_candidates');
  const lastPage = Math.max(1, Math.ceil(results.totalCount / results.pageSize));

  return (
    <section className="page">
      <div className="toolbar">
        <div className="page-header">
          <h1>Búsqueda avanzada</h1>
          <p className="muted">Filtros combinados, ANY/ALL y resultados sin duplicados.</p>
        </div>
        <div className="toolbar">
          <button
            className="button secondary"
            type="button"
            disabled={!canExport}
            onClick={exportCsv}
          >
            Exportar CSV
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
            Historial de exportaciones
          </button>
        </div>
      </div>

      {!canExport ? (
        <p className="empty-state">
          Tu rol actual no permite exportar resultados; puedes seguir buscando y revisando perfiles.
        </p>
      ) : null}

      <div className="panel grid three">
        <div className="field">
          <label htmlFor="selectedPreset">Preset guardado</label>
          <select
            id="selectedPreset"
            name="selectedPreset"
            value={selectedPresetId}
            disabled={presets.status === 'loading'}
            onChange={(e) => void loadPreset(e.target.value)}
          >
            <option value="">Selecciona un preset</option>
            {presets.presets.map((preset) => (
              <option key={preset.id} value={preset.id}>
                {preset.name}
              </option>
            ))}
          </select>
          {presets.status === 'failed' ? (
            <p className="muted" data-testid="presets-error">
              No se pudieron cargar las búsquedas guardadas.
            </p>
          ) : null}
        </div>
        <div className="field">
          <label htmlFor="presetName">Nombre para guardar</label>
          <input
            id="presetName"
            name="presetName"
            value={presetName}
            placeholder="Ej: Java + Inglés B2"
            onChange={(e) => setPresetName(e.target.value)}
          />
        </div>
        <div className="form-actions preset-actions">
          <button
            className="button secondary small"
            type="button"
            onClick={() => void savePreset()}
          >
            Guardar actual
          </button>
          <button
            className="button danger small"
            type="button"
            disabled={!selectedPresetId}
            onClick={() => void deletePreset()}
          >
            Eliminar
          </button>
        </div>
      </div>

      <div className="panel">
        <SearchFiltersPanel
          filters={filters}
          onFiltersChange={setFilters}
          collapsed={filtersCollapsed}
          onCollapsedChange={setFiltersCollapsed}
          onSearch={run}
          onClear={clear}
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
            <h2 id="export-history-title">Historial de exportaciones</h2>
            {exportBatches.length ? (
              <div className="table-wrap">
                <table>
                  <thead>
                    <tr>
                      <th>Fecha</th>
                      <th>Fichero</th>
                      <th>Filas</th>
                      <th>Estado</th>
                    </tr>
                  </thead>
                  <tbody>
                    {exportBatches.map((batch) => (
                      <tr key={batch.id}>
                        <td>{batch.exportedAt.slice(0, 19)}</td>
                        <td>{batch.fileName}</td>
                        <td>{batch.rowCount}</td>
                        <td>
                          <span className="status status--success">Completado</span>
                        </td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            ) : (
              <p className="empty-state">Sin exportaciones registradas.</p>
            )}
            <div className="modal-actions">
              <button
                className="button ghost"
                type="button"
                data-testid="close-export-history"
                onClick={() => setShowExportHistory(false)}
              >
                Cerrar
              </button>
            </div>
          </section>
        </div>
      ) : null}
    </section>
  );
}
