import type { ImportBatch } from '../../../src/app/features/admin/import/import.models';

/** A server batch as the import API returns it. */
export const batch = (overrides: Partial<ImportBatch> = {}): ImportBatch => ({
  id: 'b-1',
  state: 'validated',
  originalFileName: 'candidatos.csv',
  sizeBytes: 120,
  sha256: 'a'.repeat(64),
  rowCount: 2,
  loadedRows: 2,
  rejectedRows: 0,
  skippedRows: 0,
  failureCode: null,
  failureDetail: null,
  unresolvedValues: [],
  fileRetained: true,
  uploadedByCaller: true,
  sameFileCommittedAt: null,
  createdAt: '2026-09-16T10:00:00Z',
  updatedAt: '2026-09-16T10:00:00Z',
  validatedAt: '2026-09-16T10:00:00Z',
  committedAt: null,
  version: 7,
  ...overrides,
});
