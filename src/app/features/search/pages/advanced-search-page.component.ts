import { Component } from '@angular/core';
import { SlicePipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { AuthService } from '../../../core/auth/auth.service';
import { ToastService } from '../../../core/services/toast.service';
import { CandidateSearchService } from '../services/candidate-search.service';
import { ExportBatchRecord, ExportService } from '../services/export.service';
import { SearchPresetsService } from '../services/search-presets.service';
import {
  cloneSearchFilters,
  SearchFilters,
  SearchPreset,
  SearchResult,
} from '../models/search.models';
import { SearchFiltersComponent } from '../components/search-filters.component';
import { SearchResultsComponent } from '../components/search-results.component';
import { ObservabilityService } from '../../../core/services/observability.service';
import { toAppError } from '../../../shared/models/error.models';
import { ConfirmDialogService } from '../../../shared/components/confirm-dialog.service';

@Component({
  selector: 'rrhh-advanced-search-page',
  standalone: true,
  imports: [SearchFiltersComponent, SearchResultsComponent, FormsModule, SlicePipe],
  styles: [
    `
      .preset-actions {
        align-items: center;
        align-self: end;
      }
      .overlay {
        align-items: center;
        background: rgba(22, 33, 54, 0.45);
        display: grid;
        inset: 0;
        justify-items: center;
        padding: 16px;
        position: fixed;
        z-index: 40;
      }
      .modal {
        background: var(--bg-1);
        border: 1px solid var(--border-2);
        border-radius: var(--r-lg);
        box-shadow: 0 12px 30px rgba(0, 0, 0, 0.22);
        display: grid;
        gap: 12px;
        max-height: 80vh;
        max-width: 720px;
        overflow: auto;
        padding: 18px;
        width: 100%;
      }
      .modal h2 {
        margin: 0;
      }
      .modal-actions {
        display: flex;
        gap: 8px;
        justify-content: flex-end;
      }
    `,
  ],
  template: `
    <section class="page">
      <div class="toolbar">
        <div class="page-header">
          <h1>Búsqueda avanzada</h1>
          <p class="muted">Filtros combinados, ANY/ALL y resultados sin duplicados.</p>
        </div>
        <div class="toolbar">
          <button
            class="button secondary"
            type="button"
            [disabled]="!auth.hasPermission('export_candidates')"
            (click)="export()"
          >
            Exportar CSV
          </button>
          <button
            class="button ghost"
            type="button"
            data-testid="open-export-history"
            (click)="openExportHistory()"
          >
            Historial de exportaciones
          </button>
        </div>
      </div>

      @if (!auth.hasPermission('export_candidates')) {
        <p class="empty-state">
          Tu rol actual no permite exportar resultados; puedes seguir buscando y revisando perfiles.
        </p>
      }

      <div class="panel grid three">
        <div class="field">
          <label>Preset guardado</label>
          <select
            name="selectedPreset"
            [ngModel]="selectedPresetId"
            (ngModelChange)="loadPreset($event)"
          >
            <option value="">Selecciona un preset</option>
            @for (preset of presets; track preset.id) {
              <option [value]="preset.id">{{ preset.name }}</option>
            }
          </select>
        </div>
        <div class="field">
          <label>Nombre para guardar</label>
          <input name="presetName" [(ngModel)]="presetName" placeholder="Ej: Java + Inglés B2" />
        </div>
        <div class="form-actions preset-actions">
          <button class="button secondary small" type="button" (click)="savePreset()">
            Guardar actual
          </button>
          <button
            class="button danger small"
            type="button"
            [disabled]="!selectedPresetId"
            (click)="deletePreset()"
          >
            Eliminar
          </button>
        </div>
      </div>

      <div class="panel">
        <rrhh-search-filters
          [filters]="filters"
          [(collapsed)]="filtersCollapsed"
          (search)="run($event)"
          (clear)="clear()"
        />
      </div>
      <div class="panel">
        <rrhh-search-results [results]="results" />
      </div>

      @if (showExportHistory) {
        <div class="overlay" (click)="closeExportHistory()">
          <section
            class="modal"
            role="dialog"
            aria-modal="true"
            aria-labelledby="export-history-title"
            data-testid="export-history-modal"
            (click)="$event.stopPropagation()"
          >
            <h2 id="export-history-title">Historial de exportaciones</h2>
            @if (exportBatches.length) {
              <div class="table-wrap">
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
                    @for (batch of exportBatches; track batch.id) {
                      <tr>
                        <td>{{ batch.exportedAt | slice: 0 : 19 }}</td>
                        <td>{{ batch.fileName }}</td>
                        <td>{{ batch.rowCount }}</td>
                        <td><span class="status status--success">Completado</span></td>
                      </tr>
                    }
                  </tbody>
                </table>
              </div>
            } @else {
              <p class="empty-state">Sin exportaciones registradas.</p>
            }
            <div class="modal-actions">
              <button
                class="button ghost"
                type="button"
                data-testid="close-export-history"
                (click)="closeExportHistory()"
              >
                Cerrar
              </button>
            </div>
          </section>
        </div>
      }
    </section>
  `,
})
export class AdvancedSearchPageComponent {
  filters = this.presetsService.loadLastFilters();
  results: SearchResult[] = this.searchService.search(this.filters);
  presets: SearchPreset[] = this.presetsService.listPresets();
  exportBatches: ExportBatchRecord[] = this.exportService.listBatches();
  selectedPresetId = '';
  presetName = '';
  showExportHistory = false;
  filtersCollapsed = this.results.length > 0;

  constructor(
    readonly auth: AuthService,
    private readonly searchService: CandidateSearchService,
    private readonly presetsService: SearchPresetsService,
    private readonly exportService: ExportService,
    private readonly toast: ToastService,
    private readonly observability: ObservabilityService,
    private readonly confirmDialog: ConfirmDialogService,
  ) {}

  run(filters: SearchFilters): void {
    this.filters = cloneSearchFilters(filters);
    this.results = this.searchService.search(filters);
    this.presetsService.rememberLastFilters(this.filters);
    this.syncFiltersCollapsed();
  }

  clear(): void {
    this.filters = this.searchService.emptyFilters();
    this.results = this.searchService.search(this.filters);
    this.presetsService.rememberLastFilters(this.filters);
    this.filtersCollapsed = false;
  }

  openExportHistory(): void {
    this.refreshExportBatches();
    this.showExportHistory = true;
  }

  closeExportHistory(): void {
    this.showExportHistory = false;
  }

  savePreset(): void {
    try {
      const saved = this.presetsService.savePreset(this.presetName, this.filters);
      this.refreshPresets(saved.id);
      this.presetName = saved.name;
      this.toast.show(`Preset guardado: ${saved.name}.`, 'success');
    } catch (error) {
      this.toast.show(this.formatError(error, 'No se pudo guardar el preset.'), 'error');
    }
  }

  loadPreset(presetId: string): void {
    this.selectedPresetId = presetId;
    if (!this.selectedPresetId) {
      return;
    }
    try {
      this.filters = this.presetsService.applyPreset(this.selectedPresetId);
      this.results = this.searchService.search(this.filters);
      this.presetsService.rememberLastFilters(this.filters);
      const preset = this.presets.find((item) => item.id === this.selectedPresetId);
      this.presetName = preset?.name ?? this.presetName;
      this.refreshPresets(this.selectedPresetId);
      this.syncFiltersCollapsed();
      this.toast.show('Preset aplicado correctamente.', 'success');
    } catch (error) {
      this.toast.show(this.formatError(error, 'No se pudo cargar el preset.'), 'error');
    }
  }

  async deletePreset(): Promise<void> {
    if (!this.selectedPresetId) {
      this.toast.show('Selecciona un preset para eliminar.', 'warning');
      return;
    }
    const preset = this.presets.find((item) => item.id === this.selectedPresetId);
    const confirmDelete = await this.confirmDialog.confirm({
      title: 'Eliminar preset guardado',
      message: `Se eliminará el preset "${preset?.name ?? 'sin nombre'}".`,
      confirmText: 'Eliminar',
      cancelText: 'Cancelar',
      danger: true,
    });
    if (!confirmDelete) {
      return;
    }

    this.presetsService.removePreset(this.selectedPresetId);
    this.selectedPresetId = '';
    this.presetName = '';
    this.refreshPresets();
    this.toast.show('Preset eliminado.', 'success');
  }

  export(): void {
    if (!this.results.length) {
      this.toast.show('No hay resultados para exportar.', 'warning');
      return;
    }
    const requestId = this.observability.log('export.started', { rows: this.results.length });
    try {
      const count = this.exportService.exportCandidatesToCsv(this.results);
      this.refreshExportBatches();
      this.toast.show(
        `Exportación generada (${count} filas) sin rutas internas de Storage.`,
        'success',
      );
      this.observability.log('export.completed', { request_id: requestId, rows: count });
    } catch (error) {
      const appError = toAppError(error, 'VALIDATION_ERROR');
      this.toast.show(appError.message, 'error');
      this.observability.log('export.failed', {
        request_id: requestId,
        code: appError.code,
        message: appError.message,
      });
    }
  }

  private refreshPresets(selectedId = this.selectedPresetId): void {
    this.presets = this.presetsService.listPresets();
    this.selectedPresetId = this.presets.some((item) => item.id === selectedId) ? selectedId : '';
  }

  private refreshExportBatches(): void {
    this.exportBatches = this.exportService.listBatches();
  }

  /** Collapsed when there is something to look at, expanded when the search returned nothing. */
  private syncFiltersCollapsed(): void {
    this.filtersCollapsed = this.results.length > 0;
  }

  private formatError(error: unknown, fallback: string): string {
    return error instanceof Error && error.message ? error.message : fallback;
  }
}
