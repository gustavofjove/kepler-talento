/**
 * The candidate import API contract (KTL-17). The browser uploads a file and reads what the
 * server decided; it parses, validates and stores nothing itself.
 */

export type ImportBatchState =
  | 'uploaded'
  | 'scanning'
  | 'scanned'
  | 'validating'
  | 'validated'
  | 'committing'
  | 'committed'
  | 'infected'
  | 'unscannable'
  | 'failed'
  | 'expired';

export interface UnresolvedValue {
  family: string;
  value: string;
  occurrences: number;
}

/**
 * One batch as the API returns it. There is no storage location, and `originalFileName` is null
 * unless the caller uploaded the batch themselves.
 */
export interface ImportBatch {
  id: string;
  state: ImportBatchState;
  originalFileName: string | null;
  sizeBytes: number;
  sha256: string;
  rowCount: number | null;
  loadedRows: number;
  rejectedRows: number;
  skippedRows: number;
  failureCode: string | null;
  failureDetail: string | null;
  unresolvedValues: UnresolvedValue[];
  fileRetained: boolean;
  uploadedByCaller: boolean;
  sameFileCommittedAt: string | null;
  createdAt: string;
  updatedAt: string;
  validatedAt: string | null;
  committedAt: string | null;
  version: number;
}

export interface ImportBatchPage {
  items: ImportBatch[];
  page: number;
  pageSize: number;
  totalCount: number;
}

export type ImportRowOutcomeKind = 'loaded' | 'rejected' | 'skipped';

export type ImportPhase = 'validation' | 'commit';

/** A row number, a column name and a reason code. Never a value from the row. */
export interface ImportRowOutcome {
  rowNumber: number;
  outcome: ImportRowOutcomeKind;
  field: string | null;
  reasonCode: string | null;
}

export interface ImportRowReport {
  batchId: string;
  phase: ImportPhase;
  items: ImportRowOutcome[];
  page: number;
  pageSize: number;
  totalCount: number;
}
