import { Component } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ImportSummary } from './import.models';
import { ImportService } from './import.service';
import { ToastService } from '../../../core/services/toast.service';
import { ObservabilityService } from '../../../core/services/observability.service';
import { toAppError } from '../../../shared/models/error.models';

@Component({
  selector: 'rrhh-import-page',
  standalone: true,
  imports: [FormsModule],
  template: `
    <section class="page">
      <div class="page-header">
        <h1>Importacion Access/CSV</h1>
        <p class="muted">Flujo de carga controlada. Access no se usa como backend operativo.</p>
      </div>
      <div class="panel grid">
        <div class="field">
          <label>CSV depurado</label>
          <input type="file" accept=".csv" (change)="select($event)" />
        </div>
        <p class="muted">Paso 1: validar (dry run). Paso 2: confirmar commit.</p>
        <button class="button" type="button" [disabled]="!file || processing" (click)="process()">
          Validar (dry run)
        </button>
        @if (processing) {
          <p class="muted">Procesando fichero...</p>
        }
      </div>

      @if (!summary && !processing) {
        <p class="empty-state">
          Selecciona un CSV y ejecuta validacion dry run para habilitar commit seguro.
        </p>
      }

      @if (summary) {
        <div class="panel stack">
          <h2>Resumen</h2>
          <p><strong>Paso actual:</strong> {{ stepLabel }}</p>
          <p><strong>Origen:</strong> {{ summary.sourceName }}</p>
          <p><strong>Modo:</strong> {{ summary.dryRun ? 'Dry run' : 'Carga' }}</p>
          <p><strong>Lote:</strong> {{ summary.batchId || 'n/a' }}</p>
          <p><strong>Filas leidas:</strong> {{ summary.totalRows }}</p>
          <p><strong>Filas cargadas:</strong> {{ summary.loadedRows }}</p>
          <p><strong>Filas con error:</strong> {{ summary.errorRows }}</p>
          <p class="muted">Columnas obligatorias: {{ summary.requiredColumns.join(', ') }}</p>

          <div class="toolbar">
            <button class="button" type="button" [disabled]="!canCommit" (click)="commit()">
              Confirmar commit
            </button>
            <button
              class="button secondary"
              type="button"
              [disabled]="!summary.errors.length"
              (click)="downloadErrorsCsv()"
            >
              Descargar errores CSV
            </button>
          </div>

          @if (summary.dryRun) {
            <p class="muted">El commit se habilita solo si el dry run no tiene errores.</p>
          }
          @if (!summary.dryRun) {
            <p class="muted">
              Lote confirmado: puedes revisar historial o iniciar una nueva validacion.
            </p>
          }

          @if (summary.errors.length) {
            <div class="table-wrap">
              <table>
                <thead>
                  <tr>
                    <th>Fila</th>
                    <th>Campo</th>
                    <th>Error</th>
                  </tr>
                </thead>
                <tbody>
                  @for (
                    error of summary.errors;
                    track error.rowNumber + '-' + error.field + '-' + error.message
                  ) {
                    <tr>
                      <td>{{ error.rowNumber }}</td>
                      <td>{{ error.field }}</td>
                      <td>{{ error.message }}</td>
                    </tr>
                  }
                </tbody>
              </table>
            </div>
          } @else {
            <p class="empty-state">Sin errores de validacion.</p>
          }
        </div>
      }
    </section>
  `,
})
export class ImportPageComponent {
  file: File | null = null;
  summary: ImportSummary | null = null;
  processing = false;

  constructor(
    private readonly importService: ImportService,
    private readonly toast: ToastService,
    private readonly observability: ObservabilityService,
  ) {}

  get canCommit(): boolean {
    return (
      !!this.summary?.dryRun &&
      this.summary.errorRows === 0 &&
      !!this.summary.batchId &&
      !this.processing
    );
  }

  get stepLabel(): string {
    if (!this.summary) {
      return 'Pendiente';
    }
    if (!this.summary.dryRun) {
      return 'Commit confirmado';
    }
    if (this.summary.errorRows > 0) {
      return 'Validado con errores';
    }
    return 'Validado y listo para commit';
  }

  select(event: Event): void {
    const input = event.target as HTMLInputElement;
    this.file = input.files?.[0] ?? null;
  }

  async process(): Promise<void> {
    if (!this.file) {
      return;
    }

    this.processing = true;
    const requestId = this.observability.log('import.validate.started', {
      source_name: this.file.name,
    });
    try {
      this.summary = await this.importService.validateLocalCsv(this.file, true);
      this.toast.show('Validacion completada. Revisa errores antes de confirmar commit.', 'info');
      this.observability.log('import.validate.completed', {
        request_id: requestId,
        batch_id: this.summary.batchId,
        errors: this.summary.errorRows,
      });
    } catch (error) {
      const appError = toAppError(error, 'VALIDATION_ERROR');
      this.toast.show(appError.message, 'error');
      this.observability.log('import.validate.failed', {
        request_id: requestId,
        code: appError.code,
        message: appError.message,
      });
    } finally {
      this.processing = false;
    }
  }

  commit(): void {
    if (!this.summary?.batchId) {
      this.toast.show('No hay lote validado para confirmar.', 'warning');
      return;
    }
    if (!this.canCommit) {
      this.toast.show('No se puede confirmar commit con errores.', 'warning');
      return;
    }

    const committed = this.importService.markCommitted(this.summary.batchId);
    this.summary = {
      ...this.summary,
      dryRun: false,
      loadedRows: committed.loadedRows,
    };
    this.toast.show(`Commit confirmado para lote ${committed.id}.`, 'success');
    this.observability.log('import.commit.completed', {
      batch_id: committed.id,
      loaded_rows: committed.loadedRows,
    });
  }

  downloadErrorsCsv(): void {
    if (!this.summary?.errors.length) {
      this.toast.show('No hay errores para descargar.', 'warning');
      return;
    }

    const header = ['row_number', 'field', 'message'];
    const lines = this.summary.errors.map((error) =>
      [String(error.rowNumber), error.field, error.message]
        .map((value) => `"${value.replace(/"/g, '""')}"`)
        .join(','),
    );
    const csv = [header.join(','), ...lines].join('\n');
    const url = URL.createObjectURL(new Blob([csv], { type: 'text/csv' }));
    const link = document.createElement('a');
    link.href = url;
    link.download = 'import-errors.csv';
    link.click();
    URL.revokeObjectURL(url);
    this.toast.show('CSV de errores descargado.', 'success');
    this.observability.log('import.errors_csv.downloaded', {
      rows: this.summary.errors.length,
    });
  }
}
