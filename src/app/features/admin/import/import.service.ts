import type { ApiTransport } from '../../../core/http/api-transport';
import { TranslatableError } from '../../../core/i18n/translatable-error';
import { isTransient, POLL_INTERVAL_MS, POLL_MAX_ATTEMPTS } from './import.logic';
import type { ImportBatch, ImportBatchPage, ImportPhase, ImportRowReport } from './import.models';

export interface PollOptions {
  intervalMs?: number;
  maxAttempts?: number;
  signal?: AbortSignal;
  onProgress?: (batch: ImportBatch) => void;
}

const wait = (ms: number, signal?: AbortSignal) =>
  new Promise<void>((resolve, reject) => {
    const timer = setTimeout(resolve, ms);
    signal?.addEventListener(
      'abort',
      () => {
        clearTimeout(timer);
        reject(new TranslatableError('admin.import.errors.cancelled'));
      },
      { once: true },
    );
  });

/**
 * The candidate import, as an API gateway (KTL-17 design D10).
 *
 * The CSV parser, the validation rules, the `localStorage` batch store and the `Math.random()`
 * id generator of the old stub are gone rather than kept as a fallback: a fallback would be a
 * screen that sometimes validates against the real rules and sometimes against the stub's.
 */
export class ImportService {
  constructor(private readonly api: ApiTransport) {}

  async upload(file: File | null): Promise<ImportBatch> {
    if (!file) {
      throw new TranslatableError('admin.import.errors.noFile');
    }
    const form = new FormData();
    form.append('file', file);
    return this.api.request<ImportBatch>('/import/batches', {
      method: 'POST',
      body: form,
      timeoutMs: 60_000,
    });
  }

  getBatch(id: string): Promise<ImportBatch> {
    return this.api.request<ImportBatch>(`/import/batches/${encodeURIComponent(id)}`);
  }

  validate(batch: ImportBatch): Promise<ImportBatch> {
    return this.transition(batch, 'validation');
  }

  commit(batch: ImportBatch): Promise<ImportBatch> {
    return this.transition(batch, 'commit');
  }

  listBatches(page = 1, pageSize = 20): Promise<ImportBatchPage> {
    return this.api.request<ImportBatchPage>(`/import/batches?page=${page}&pageSize=${pageSize}`);
  }

  getRowReport(id: string, page = 1, pageSize = 50, phase?: ImportPhase): Promise<ImportRowReport> {
    const query = new URLSearchParams({ page: String(page), pageSize: String(pageSize) });
    if (phase) {
      query.set('phase', phase);
    }
    return this.api.request<ImportRowReport>(
      `/import/batches/${encodeURIComponent(id)}/rows?${query.toString()}`,
    );
  }

  /**
   * Polls a batch until it leaves the transient states, with a bounded interval and a give-up.
   * Giving up throws; it never pretends the batch finished.
   */
  async waitUntilSettled(batch: ImportBatch, options: PollOptions = {}): Promise<ImportBatch> {
    const interval = options.intervalMs ?? POLL_INTERVAL_MS;
    const maxAttempts = options.maxAttempts ?? POLL_MAX_ATTEMPTS;
    let current = batch;
    for (let attempt = 0; attempt < maxAttempts; attempt += 1) {
      if (!isTransient(current.state)) {
        return current;
      }
      await wait(interval, options.signal);
      current = await this.getBatch(current.id);
      options.onProgress?.(current);
    }
    if (!isTransient(current.state)) {
      return current;
    }
    throw new TranslatableError('admin.import.errors.pollTimeout');
  }

  private transition(batch: ImportBatch, step: 'validation' | 'commit'): Promise<ImportBatch> {
    return this.api.request<ImportBatch>(
      `/import/batches/${encodeURIComponent(batch.id)}/${step}`,
      {
        method: 'POST',
        body: JSON.stringify({ version: batch.version }),
      },
    );
  }
}
