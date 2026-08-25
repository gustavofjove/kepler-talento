import { useState, type ChangeEvent } from 'react';
import { useServices } from '../../../core/di/services-context';
import { toAppError } from '../../../shared/models/error.models';
import type { ImportSummary } from './import.models';

export function ImportPage() {
  const { importService, toastService, observabilityService } = useServices();
  const [file, setFile] = useState<File | null>(null);
  const [summary, setSummary] = useState<ImportSummary | null>(null);
  const [processing, setProcessing] = useState(false);

  const canCommit =
    !!summary?.dryRun && summary.errorRows === 0 && !!summary.batchId && !processing;

  const stepLabel = ((): string => {
    if (!summary) {
      return 'Pendiente';
    }
    if (!summary.dryRun) {
      return 'Commit confirmado';
    }
    if (summary.errorRows > 0) {
      return 'Validado con errores';
    }
    return 'Validado y listo para commit';
  })();

  const select = (event: ChangeEvent<HTMLInputElement>): void => {
    setFile(event.target.files?.[0] ?? null);
  };

  const process = async (): Promise<void> => {
    if (!file) {
      return;
    }
    setProcessing(true);
    const requestId = observabilityService.log('import.validate.started', {
      source_name: file.name,
    });
    try {
      const result = await importService.validateLocalCsv(file, true);
      setSummary(result);
      toastService.show('Validación completada. Revisa errores antes de confirmar commit.', 'info');
      observabilityService.log('import.validate.completed', {
        request_id: requestId,
        batch_id: result.batchId,
        errors: result.errorRows,
      });
    } catch (error) {
      const appError = toAppError(error, 'VALIDATION_ERROR');
      toastService.show(appError.message, 'error');
      observabilityService.log('import.validate.failed', {
        request_id: requestId,
        code: appError.code,
        message: appError.message,
      });
    } finally {
      setProcessing(false);
    }
  };

  const commit = (): void => {
    if (!summary?.batchId) {
      toastService.show('No hay lote validado para confirmar.', 'warning');
      return;
    }
    if (!canCommit) {
      toastService.show('No se puede confirmar commit con errores.', 'warning');
      return;
    }
    const committed = importService.markCommitted(summary.batchId);
    setSummary({ ...summary, dryRun: false, loadedRows: committed.loadedRows });
    toastService.show(`Commit confirmado para lote ${committed.id}.`, 'success');
    observabilityService.log('import.commit.completed', {
      batch_id: committed.id,
      loaded_rows: committed.loadedRows,
    });
  };

  const downloadErrorsCsv = (): void => {
    if (!summary?.errors.length) {
      toastService.show('No hay errores para descargar.', 'warning');
      return;
    }
    const header = ['row_number', 'field', 'message'];
    const lines = summary.errors.map((error) =>
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
    toastService.show('CSV de errores descargado.', 'success');
    observabilityService.log('import.errors_csv.downloaded', { rows: summary.errors.length });
  };

  return (
    <section className="page">
      <div className="page-header">
        <h1>Importación Access/CSV</h1>
        <p className="muted">Flujo de carga controlada. Access no se usa como backend operativo.</p>
      </div>
      <div className="panel grid">
        <div className="field">
          <label htmlFor="csv">CSV depurado</label>
          <input id="csv" type="file" accept=".csv" onChange={select} />
        </div>
        <p className="muted">Paso 1: validar (dry run). Paso 2: confirmar commit.</p>
        <button className="button" type="button" disabled={!file || processing} onClick={process}>
          Validar (dry run)
        </button>
        {processing ? <p className="muted">Procesando fichero...</p> : null}
      </div>

      {!summary && !processing ? (
        <p className="empty-state">
          Selecciona un CSV y ejecuta validación dry run para habilitar commit seguro.
        </p>
      ) : null}

      {summary ? (
        <div className="panel stack">
          <h2>Resumen</h2>
          <p>
            <strong>Paso actual:</strong> {stepLabel}
          </p>
          <p>
            <strong>Origen:</strong> {summary.sourceName}
          </p>
          <p>
            <strong>Modo:</strong> {summary.dryRun ? 'Dry run' : 'Carga'}
          </p>
          <p>
            <strong>Lote:</strong> {summary.batchId || 'n/a'}
          </p>
          <p>
            <strong>Filas leídas:</strong> {summary.totalRows}
          </p>
          <p>
            <strong>Filas cargadas:</strong> {summary.loadedRows}
          </p>
          <p>
            <strong>Filas con error:</strong> {summary.errorRows}
          </p>
          <p className="muted">Columnas obligatorias: {summary.requiredColumns.join(', ')}</p>

          <div className="toolbar">
            <button className="button" type="button" disabled={!canCommit} onClick={commit}>
              Confirmar commit
            </button>
            <button
              className="button secondary"
              type="button"
              disabled={!summary.errors.length}
              onClick={downloadErrorsCsv}
            >
              Descargar errores CSV
            </button>
          </div>

          {summary.dryRun ? (
            <p className="muted">El commit se habilita solo si el dry run no tiene errores.</p>
          ) : (
            <p className="muted">
              Lote confirmado: puedes revisar historial o iniciar una nueva validación.
            </p>
          )}

          {summary.errors.length ? (
            <div className="table-wrap">
              <table>
                <thead>
                  <tr>
                    <th>Fila</th>
                    <th>Campo</th>
                    <th>Error</th>
                  </tr>
                </thead>
                <tbody>
                  {summary.errors.map((error) => (
                    <tr key={`${error.rowNumber}-${error.field}-${error.message}`}>
                      <td>{error.rowNumber}</td>
                      <td>{error.field}</td>
                      <td>{error.message}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          ) : (
            <p className="empty-state">Sin errores de validación.</p>
          )}
        </div>
      ) : null}
    </section>
  );
}
