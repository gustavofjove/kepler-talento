import { useEffect, useState } from 'react';
import { usePermission, useServices } from '../../../core/di/services-context';
import { toAppError } from '../../../shared/models/error.models';
import type { ExportBatchRecord } from '../services/export.service';
import { SearchFilters as SearchFiltersPanel } from '../components/search-filters';
import { SearchResults } from '../components/search-results';
import {
  cloneSearchFilters,
  type SearchFilters,
  type SearchPreset,
  type SearchResult,
} from '../models/search.models';
import '../../../shared/components/modal.css';
import './advanced-search-page.css';

export function AdvancedSearchPage() {
  const {
    candidateSearchService,
    searchPresetsService,
    exportService,
    toastService,
    observabilityService,
    confirmDialogService,
  } = useServices();

  // filters and results are deliberately separate. Editing a filter must NOT
  // re-run the search - results only change in run/clear/loadPreset, exactly as
  // the Angular version behaved (FR-4).
  const [filters, setFilters] = useState<SearchFilters>(() =>
    searchPresetsService.loadLastFilters(),
  );
  // Searching now awaits the API-backed aggregates, so the initial results arrive from an
  // effect rather than from lazy state.
  const [results, setResults] = useState<SearchResult[]>([]);
  const [presets, setPresets] = useState<SearchPreset[]>(() => searchPresetsService.listPresets());
  const [exportBatches, setExportBatches] = useState<ExportBatchRecord[]>(() =>
    exportService.listBatches(),
  );
  const [selectedPresetId, setSelectedPresetId] = useState('');
  const [presetName, setPresetName] = useState('');
  const [showExportHistory, setShowExportHistory] = useState(false);
  // The filter panel starts open, and collapses when the user runs a search.
  //
  // It used to start collapsed whenever the restored search had results, decided
  // synchronously at mount because the results came from browser storage. Now that the
  // restored search awaits the API, that decision would land after the page was
  // interactive and yank the panel shut under a user already typing in it. Collapsing
  // only on a deliberate search keeps the "results are what matters now" behaviour
  // without ever overruling the user.
  const [filtersCollapsed, setFiltersCollapsed] = useState(false);

  useEffect(() => {
    let cancelled = false;
    void candidateSearchService.search(searchPresetsService.loadLastFilters()).then((found) => {
      if (!cancelled) {
        setResults(found);
      }
    });
    return () => {
      cancelled = true;
    };
  }, [candidateSearchService, searchPresetsService]);

  const formatError = (error: unknown, fallback: string): string =>
    error instanceof Error && error.message ? error.message : fallback;

  const refreshPresets = (selectedId: string): void => {
    const next = searchPresetsService.listPresets();
    setPresets(next);
    setSelectedPresetId(next.some((item) => item.id === selectedId) ? selectedId : '');
  };

  const run = async (next: SearchFilters): Promise<void> => {
    const cloned = cloneSearchFilters(next);
    const found = await candidateSearchService.search(next);
    setFilters(cloned);
    setResults(found);
    searchPresetsService.rememberLastFilters(cloned);
    setFiltersCollapsed(found.length > 0);
  };

  const clear = async (): Promise<void> => {
    const empty = candidateSearchService.emptyFilters();
    setFilters(empty);
    setResults(await candidateSearchService.search(empty));
    searchPresetsService.rememberLastFilters(empty);
    setFiltersCollapsed(false);
  };

  const savePreset = (): void => {
    try {
      const saved = searchPresetsService.savePreset(presetName, filters);
      refreshPresets(saved.id);
      setPresetName(saved.name);
      toastService.show(`Preset guardado: ${saved.name}.`, 'success');
    } catch (error) {
      toastService.show(formatError(error, 'No se pudo guardar el preset.'), 'error');
    }
  };

  const loadPreset = async (presetId: string): Promise<void> => {
    setSelectedPresetId(presetId);
    if (!presetId) {
      return;
    }
    try {
      const applied = searchPresetsService.applyPreset(presetId);
      const found = await candidateSearchService.search(applied);
      setFilters(applied);
      setResults(found);
      searchPresetsService.rememberLastFilters(applied);
      const preset = presets.find((item) => item.id === presetId);
      if (preset?.name) {
        setPresetName(preset.name);
      }
      refreshPresets(presetId);
      setFiltersCollapsed(found.length > 0);
      toastService.show('Preset aplicado correctamente.', 'success');
    } catch (error) {
      toastService.show(formatError(error, 'No se pudo cargar el preset.'), 'error');
    }
  };

  const deletePreset = async (): Promise<void> => {
    if (!selectedPresetId) {
      toastService.show('Selecciona un preset para eliminar.', 'warning');
      return;
    }
    const preset = presets.find((item) => item.id === selectedPresetId);
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
    searchPresetsService.removePreset(selectedPresetId);
    setSelectedPresetId('');
    setPresetName('');
    refreshPresets('');
    toastService.show('Preset eliminado.', 'success');
  };

  const exportCsv = (): void => {
    if (!results.length) {
      toastService.show('No hay resultados para exportar.', 'warning');
      return;
    }
    const requestId = observabilityService.log('export.started', { rows: results.length });
    try {
      const count = exportService.exportCandidatesToCsv(results);
      setExportBatches(exportService.listBatches());
      toastService.show(
        `Exportación generada (${count} filas) sin rutas internas de Storage.`,
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
            onChange={(e) => loadPreset(e.target.value)}
          >
            <option value="">Selecciona un preset</option>
            {presets.map((preset) => (
              <option key={preset.id} value={preset.id}>
                {preset.name}
              </option>
            ))}
          </select>
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
          <button className="button secondary small" type="button" onClick={savePreset}>
            Guardar actual
          </button>
          <button
            className="button danger small"
            type="button"
            disabled={!selectedPresetId}
            onClick={deletePreset}
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
        <SearchResults results={results} />
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
