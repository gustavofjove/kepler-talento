export interface ImportSummary {
  sourceName: string;
  dryRun: boolean;
  totalRows: number;
  loadedRows: number;
  errorRows: number;
  requiredColumns: string[];
  errors: ImportRowError[];
  batchId?: string;
}

export interface ImportRowError {
  rowNumber: number;
  field: string;
  message: string;
}

export type ImportBatchStatus = 'validated' | 'committed';

export interface ImportBatchRecord {
  id: string;
  sourceName: string;
  dryRun: boolean;
  status: ImportBatchStatus;
  totalRows: number;
  loadedRows: number;
  errorRows: number;
  createdAt: string;
  committedAt?: string;
}
