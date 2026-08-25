import { ImportBatchRecord, ImportRowError, ImportSummary } from './import.models';
import { AppError } from '../../../shared/models/error.models';

const REQUIRED_COLUMNS = ['first_name', 'last_name'];
const IMPORT_BATCHES_STORAGE_KEY = 'rrhh.import.batches.v1';
export const MAX_IMPORT_ROWS = 2000;

export class ImportService {
  async validateLocalCsv(file: File, dryRun: boolean): Promise<ImportSummary> {
    const content = await file.text();
    return this.validateCsvContent(file.name, content, dryRun);
  }

  validateCsvContent(sourceName: string, content: string, dryRun: boolean): ImportSummary {
    const rows = this.parseCsv(content);
    if (!rows.length) {
      return {
        sourceName,
        dryRun,
        totalRows: 0,
        loadedRows: 0,
        errorRows: 0,
        requiredColumns: REQUIRED_COLUMNS,
        errors: [],
      };
    }

    const [header, ...dataRows] = rows;
    if (dataRows.length > MAX_IMPORT_ROWS) {
      throw new AppError(
        'VALIDATION_ERROR',
        `El límite máximo por importación es ${MAX_IMPORT_ROWS} filas.`,
      );
    }

    const headerIndex = this.headerIndex(header);
    const errors: ImportRowError[] = [];

    for (const required of REQUIRED_COLUMNS) {
      if (headerIndex[required] === undefined) {
        errors.push({
          rowNumber: 1,
          field: required,
          message: `Falta la columna obligatoria: ${required}.`,
        });
      }
    }

    dataRows.forEach((row, index) => {
      const rowNumber = index + 2;
      const firstName = this.getValue(row, headerIndex, 'first_name');
      const lastName = this.getValue(row, headerIndex, 'last_name');
      const email = this.getValue(row, headerIndex, 'email');

      if (!firstName) {
        errors.push({ rowNumber, field: 'first_name', message: 'Nombre obligatorio.' });
      }

      if (!lastName) {
        errors.push({ rowNumber, field: 'last_name', message: 'Apellidos obligatorios.' });
      }

      if (email && !/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(email)) {
        errors.push({ rowNumber, field: 'email', message: 'Email inválido.' });
      }
    });

    const erroredRows = new Set(
      errors.filter((error) => error.rowNumber > 1).map((e) => e.rowNumber),
    );
    const totalRows = dataRows.length;

    const summary: ImportSummary = {
      sourceName,
      dryRun,
      totalRows,
      loadedRows: dryRun ? 0 : Math.max(totalRows - erroredRows.size, 0),
      errorRows: erroredRows.size,
      requiredColumns: REQUIRED_COLUMNS,
      errors,
    };

    const status = dryRun ? 'validated' : 'committed';
    const batch = this.appendBatch(summary, status);

    return {
      ...summary,
      batchId: batch.id,
    };
  }

  listBatches(): ImportBatchRecord[] {
    return this.readBatches().sort((a, b) => b.createdAt.localeCompare(a.createdAt));
  }

  markCommitted(batchId: string): ImportBatchRecord {
    const batches = this.readBatches();
    const batch = batches.find((item) => item.id === batchId);
    if (!batch) {
      throw new Error('Lote de importación no encontrado.');
    }

    const updated: ImportBatchRecord = {
      ...batch,
      status: 'committed',
      dryRun: false,
      loadedRows: Math.max(batch.totalRows - batch.errorRows, 0),
      committedAt: new Date().toISOString(),
    };
    this.writeBatches(batches.map((item) => (item.id === batchId ? updated : item)));
    return updated;
  }

  private headerIndex(header: string[]): Record<string, number> {
    return header.reduce<Record<string, number>>((acc, key, index) => {
      acc[key.trim().toLowerCase()] = index;
      return acc;
    }, {});
  }

  private getValue(row: string[], indexByName: Record<string, number>, key: string): string {
    const index = indexByName[key];
    if (index === undefined) {
      return '';
    }
    return (row[index] || '').trim();
  }

  private parseCsv(content: string): string[][] {
    return content
      .split(/\r?\n/)
      .filter((line) => line.trim().length > 0)
      .map((line) => this.parseCsvLine(line));
  }

  private parseCsvLine(line: string): string[] {
    const values: string[] = [];
    let current = '';
    let inQuotes = false;

    for (let i = 0; i < line.length; i += 1) {
      const char = line[i];
      const next = line[i + 1];

      if (char === '"') {
        if (inQuotes && next === '"') {
          current += '"';
          i += 1;
        } else {
          inQuotes = !inQuotes;
        }
        continue;
      }

      if (char === ',' && !inQuotes) {
        values.push(current.trim());
        current = '';
        continue;
      }

      current += char;
    }

    values.push(current.trim());
    return values;
  }

  private appendBatch(
    summary: ImportSummary,
    status: 'validated' | 'committed',
  ): ImportBatchRecord {
    const now = new Date().toISOString();
    const batch: ImportBatchRecord = {
      id: this.generateId(),
      sourceName: summary.sourceName,
      dryRun: summary.dryRun,
      status,
      totalRows: summary.totalRows,
      loadedRows: summary.loadedRows,
      errorRows: summary.errorRows,
      createdAt: now,
      committedAt: status === 'committed' ? now : undefined,
    };

    this.writeBatches([...this.readBatches(), batch]);
    return batch;
  }

  private readBatches(): ImportBatchRecord[] {
    try {
      const raw = localStorage.getItem(IMPORT_BATCHES_STORAGE_KEY);
      if (!raw) {
        return [];
      }

      const parsed = JSON.parse(raw) as ImportBatchRecord[];
      if (!Array.isArray(parsed)) {
        return [];
      }

      return parsed.filter((item) => item && typeof item.id === 'string');
    } catch {
      return [];
    }
  }

  private writeBatches(batches: ImportBatchRecord[]): void {
    localStorage.setItem(IMPORT_BATCHES_STORAGE_KEY, JSON.stringify(batches));
  }

  private generateId(): string {
    return `imp_${Math.random().toString(36).slice(2, 10)}`;
  }
}
