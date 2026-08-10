import { Injectable } from '@angular/core';
import { SearchResult } from '../models/search.models';
import { AppError } from '../../../shared/models/error.models';

export interface ExportBatchRecord {
  id: string;
  fileName: string;
  rowCount: number;
  exportedAt: string;
}

const EXPORT_BATCHES_STORAGE_KEY = 'rrhh.export.batches.v1';
export const MAX_EXPORT_ROWS = 1000;

@Injectable({ providedIn: 'root' })
export class ExportService {
  exportCandidatesToCsv(results: SearchResult[], fileName = 'candidatos.csv'): number {
    if (results.length > MAX_EXPORT_ROWS) {
      throw new AppError(
        'VALIDATION_ERROR',
        `El limite maximo por exportacion es ${MAX_EXPORT_ROWS} filas.`,
      );
    }

    const rows = results.map((item) => ({
      nombre: item.firstName,
      apellidos: item.lastName,
      telefono: item.phone,
      email: item.email,
      estado: item.status,
      cv_disponible: item.hasPrimaryCv ? 'si' : 'no',
      actualizado_en: item.updatedAt,
    }));

    const header = Object.keys(
      rows[0] ?? {
        nombre: '',
        apellidos: '',
        telefono: '',
        email: '',
        estado: '',
        cv_disponible: '',
        actualizado_en: '',
      },
    );

    const csv = [
      header.join(','),
      ...rows.map((row) =>
        header
          .map((column) => this.escapeCsvValue(String(row[column as keyof typeof row] ?? '')))
          .join(','),
      ),
    ].join('\n');

    const url = URL.createObjectURL(new Blob([csv], { type: 'text/csv' }));
    const link = document.createElement('a');
    link.href = url;
    link.download = fileName;
    link.click();
    URL.revokeObjectURL(url);

    this.appendBatch({
      fileName,
      rowCount: rows.length,
    });

    return rows.length;
  }

  listBatches(): ExportBatchRecord[] {
    return this.readBatches().sort((a, b) => b.exportedAt.localeCompare(a.exportedAt));
  }

  private escapeCsvValue(value: string): string {
    return `"${value.replace(/"/g, '""')}"`;
  }

  private appendBatch(data: { fileName: string; rowCount: number }): void {
    const batch: ExportBatchRecord = {
      id: this.generateId(),
      fileName: data.fileName,
      rowCount: data.rowCount,
      exportedAt: new Date().toISOString(),
    };
    this.writeBatches([...this.readBatches(), batch]);
  }

  private readBatches(): ExportBatchRecord[] {
    try {
      const raw = localStorage.getItem(EXPORT_BATCHES_STORAGE_KEY);
      if (!raw) {
        return [];
      }

      const parsed = JSON.parse(raw) as ExportBatchRecord[];
      if (!Array.isArray(parsed)) {
        return [];
      }

      return parsed.filter((item) => item && typeof item.id === 'string');
    } catch {
      return [];
    }
  }

  private writeBatches(batches: ExportBatchRecord[]): void {
    localStorage.setItem(EXPORT_BATCHES_STORAGE_KEY, JSON.stringify(batches));
  }

  private generateId(): string {
    return `exp_${Math.random().toString(36).slice(2, 10)}`;
  }
}
