import { AppError } from '../../../shared/models/error.models';
import type { ImportBatch, ImportBatchState, ImportRowOutcome } from './import.models';

/** Refusal codes the API returns that the page has its own sentence for. */
export const KNOWN_REFUSALS = [
  'import.batch.not_found',
  'import.batch.not_ready',
  'import.batch.refused',
  'import.batch.not_validated',
  'import.batch.has_rejected_rows',
  'import.batch.expired',
  'import.batch.version_conflict',
  'import.file.missing',
  'import.file.empty',
  'import.file.too_large',
  'import.file.type_not_allowed',
] as const;

/**
 * The translation key for an API refusal, or null when the page has no sentence of its own for
 * it. Looks at the problem's code and, for a validation problem, at the first field issue's code.
 */
export function importErrorKey(error: unknown): string | null {
  if (!(error instanceof AppError)) {
    return null;
  }
  const issues = (error.details as { errors?: { code?: string }[] } | undefined)?.errors;
  const candidates = [
    error.backendCode,
    ...(Array.isArray(issues) ? issues.map((i) => i?.code) : []),
  ];
  const known = candidates.find((code): code is (typeof KNOWN_REFUSALS)[number] =>
    KNOWN_REFUSALS.includes(code as (typeof KNOWN_REFUSALS)[number]),
  );
  return known ? `admin.import.errors.${known}` : null;
}

/** States a worker moves on by itself; the page polls while a batch is in one. */
export const TRANSIENT_STATES: readonly ImportBatchState[] = [
  'uploaded',
  'scanning',
  'validating',
  'committing',
];

/** The scanner refused the file. Terminal: no retry admits it. */
export const REFUSED_STATES: readonly ImportBatchState[] = ['infected', 'unscannable'];

/** Bounded polling (design D10): about two minutes, then the page stops and says so. */
export const POLL_INTERVAL_MS = 1500;
export const POLL_MAX_ATTEMPTS = 80;

export const ROW_REPORT_PAGE_SIZE = 50;

export function isTransient(state: ImportBatchState): boolean {
  return TRANSIENT_STATES.includes(state);
}

export function isRefused(state: ImportBatchState): boolean {
  return REFUSED_STATES.includes(state);
}

export function canCommit(batch: ImportBatch | null): boolean {
  return !!batch && batch.state === 'validated' && batch.rejectedRows === 0 && batch.fileRetained;
}

/**
 * The translation key for a stable reason code. Codes are translated by the page, never shown
 * raw; an unknown code falls back to a generic sentence rather than leaking the code.
 */
export function reasonKey(code: string | null): string {
  return code ? `admin.import.reason.${code}` : 'admin.import.reason.unknown';
}

/**
 * The downloadable report: row, column and reason code only. It is built from the server's
 * report, which carries no values, so the file cannot carry one either.
 */
export function rowReportCsv(rows: ImportRowOutcome[]): string {
  const header = ['row_number', 'outcome', 'field', 'reason_code'];
  const lines = rows.map((row) =>
    [String(row.rowNumber), row.outcome, row.field ?? '', row.reasonCode ?? '']
      .map((value) => `"${value.replace(/"/g, '""')}"`)
      .join(','),
  );
  return [header.join(','), ...lines].join('\n');
}
