import { readFileSync } from 'node:fs';
import { join } from 'node:path';
import { i18n } from '../../src/app/core/i18n/i18n';
import { TranslatableError } from '../../src/app/core/i18n/translatable-error';
import {
  canCommit,
  importErrorKey,
  KNOWN_REFUSALS,
  reasonKey,
  rowReportCsv,
} from '../../src/app/features/admin/import/import.logic';
import { ImportService } from '../../src/app/features/admin/import/import.service';
import { AppError } from '../../src/app/shared/models/error.models';
import { batch } from './support/import-doubles';
import { repoRoot } from '../repo-root';

describe('ImportService', () => {
  it('uploads the file as multipart form data and returns the server batch', async () => {
    const api = { request: vi.fn().mockResolvedValue(batch({ state: 'uploaded' })) };
    const service = new ImportService(api as never);
    const file = new File(['first_name,last_name,email\n'], 'candidatos.csv', { type: 'text/csv' });

    const uploaded = await service.upload(file);

    expect(uploaded.state).toBe('uploaded');
    const [path, options] = api.request.mock.calls[0];
    expect(path).toBe('/import/batches');
    expect(options.method).toBe('POST');
    expect(options.body).toBeInstanceOf(FormData);
    expect((options.body as FormData).get('file')).toBe(file);
  });

  it('refuses to upload without a file and makes no request', async () => {
    const api = { request: vi.fn() };
    const service = new ImportService(api as never);

    await expect(service.upload(null)).rejects.toMatchObject({ key: 'admin.import.errors.noFile' });
    expect(api.request).not.toHaveBeenCalled();
  });

  it('sends the version it read with validate and commit', async () => {
    const api = { request: vi.fn().mockResolvedValue(batch()) };
    const service = new ImportService(api as never);

    await service.validate(batch({ state: 'scanned', version: 3 }));
    await service.commit(batch({ version: 9 }));

    expect(api.request.mock.calls[0][0]).toBe('/import/batches/b-1/validation');
    expect(api.request.mock.calls[0][1]).toMatchObject({ method: 'POST', body: '{"version":3}' });
    expect(api.request.mock.calls[1][0]).toBe('/import/batches/b-1/commit');
    expect(api.request.mock.calls[1][1]).toMatchObject({ method: 'POST', body: '{"version":9}' });
  });

  it('pages the batch history and the row report', async () => {
    const api = {
      request: vi.fn().mockResolvedValue({ items: [], page: 2, pageSize: 50, totalCount: 0 }),
    };
    const service = new ImportService(api as never);

    await service.listBatches(2, 10);
    await service.getRowReport('b-1', 3, 50, 'commit');

    expect(api.request.mock.calls[0][0]).toBe('/import/batches?page=2&pageSize=10');
    expect(api.request.mock.calls[1][0]).toBe(
      '/import/batches/b-1/rows?page=3&pageSize=50&phase=commit',
    );
  });

  it('polls a transient batch until it settles', async () => {
    const api = {
      request: vi
        .fn()
        .mockResolvedValueOnce(batch({ state: 'scanning' }))
        .mockResolvedValueOnce(batch({ state: 'scanned' })),
    };
    const service = new ImportService(api as never);
    const progress = vi.fn();

    const settled = await service.waitUntilSettled(batch({ state: 'uploaded' }), {
      intervalMs: 1,
      onProgress: progress,
    });

    expect(settled.state).toBe('scanned');
    expect(api.request).toHaveBeenCalledTimes(2);
    expect(progress).toHaveBeenCalledTimes(2);
  });

  it('returns a settled batch without polling at all', async () => {
    const api = { request: vi.fn() };
    const service = new ImportService(api as never);

    await service.waitUntilSettled(batch({ state: 'infected' }), { intervalMs: 1 });

    expect(api.request).not.toHaveBeenCalled();
  });

  it('gives up after a bounded number of polls and says so', async () => {
    const api = { request: vi.fn().mockResolvedValue(batch({ state: 'committing' })) };
    const service = new ImportService(api as never);

    await expect(
      service.waitUntilSettled(batch({ state: 'committing' }), { intervalMs: 1, maxAttempts: 3 }),
    ).rejects.toMatchObject({ key: 'admin.import.errors.pollTimeout' });
    expect(api.request).toHaveBeenCalledTimes(3);
  });

  it('stops polling when cancelled', async () => {
    const api = { request: vi.fn().mockResolvedValue(batch({ state: 'scanning' })) };
    const service = new ImportService(api as never);
    const controller = new AbortController();

    const pending = service.waitUntilSettled(batch({ state: 'scanning' }), {
      intervalMs: 50,
      signal: controller.signal,
    });
    controller.abort();

    await expect(pending).rejects.toBeInstanceOf(TranslatableError);
  });

  it('propagates an API refusal unchanged', async () => {
    const refusal = new AppError('CONFLICT', 'x', undefined, undefined, 'import.batch.not_ready');
    const api = { request: vi.fn().mockRejectedValue(refusal) };
    const service = new ImportService(api as never);

    await expect(service.validate(batch({ state: 'scanning' }))).rejects.toBe(refusal);
  });

  it('keeps nothing in browser storage', async () => {
    localStorage.clear();
    const api = { request: vi.fn().mockResolvedValue(batch()) };
    const service = new ImportService(api as never);

    await service.upload(new File(['a'], 'a.csv'));
    await service.commit(batch());
    await service.listBatches();

    expect(localStorage.length).toBe(0);
  });
});

describe('import page logic', () => {
  it('allows a commit only for a validated batch with no rejected rows and its file retained', () => {
    expect(canCommit(batch())).toBe(true);
    expect(canCommit(batch({ rejectedRows: 1 }))).toBe(false);
    expect(canCommit(batch({ state: 'committed' }))).toBe(false);
    expect(canCommit(batch({ fileRetained: false }))).toBe(false);
    expect(canCommit(null)).toBe(false);
  });

  it('maps a known API refusal, including a field issue code, to its own sentence', () => {
    expect(
      importErrorKey(new AppError('CONFLICT', 'x', undefined, undefined, 'import.batch.expired')),
    ).toBe('admin.import.errors.import.batch.expired');
    expect(
      importErrorKey(
        new AppError(
          'VALIDATION_ERROR',
          'x',
          { errors: [{ code: 'import.file.too_large' }] },
          undefined,
          'validation.failed',
        ),
      ),
    ).toBe('admin.import.errors.import.file.too_large');
    expect(
      importErrorKey(new AppError('CONFLICT', 'x', undefined, undefined, 'other.code')),
    ).toBeNull();
    expect(importErrorKey(new Error('x'))).toBeNull();
  });

  it('builds a report file from row numbers, columns and codes only', () => {
    const csv = rowReportCsv([
      { rowNumber: 3, outcome: 'rejected', field: 'email', reasonCode: 'email.invalid' },
      { rowNumber: 4, outcome: 'loaded', field: null, reasonCode: null },
    ]);

    expect(csv.split('\n')).toEqual([
      'row_number,outcome,field,reason_code',
      '"3","rejected","email","email.invalid"',
      '"4","loaded","",""',
    ]);
  });

  it('has Spanish copy for every reason code and refusal the API defines', () => {
    const source = readFileSync(
      join(repoRoot, 'backend/Domain/Import/ImportReasonCodes.cs'),
      'utf8',
    );
    const codes = [...source.matchAll(/public const string \w+ = "([^"]+)";/g)].map(
      (match) => match[1],
    );

    expect(codes.length).toBeGreaterThan(20);
    for (const code of codes) {
      expect(i18n.exists(reasonKey(code))).toBe(true);
    }
    for (const refusal of KNOWN_REFUSALS) {
      expect(i18n.exists(`admin.import.errors.${refusal}`)).toBe(true);
    }
  });

  it('has Spanish copy for every batch state the API defines', () => {
    const source = readFileSync(join(repoRoot, 'backend/Domain/Import/ImportBatch.cs'), 'utf8');
    const block = source.slice(
      source.indexOf('class ImportBatchStates'),
      source.indexOf('public static readonly'),
    );
    const states = [...block.matchAll(/= "([a-z]+)";/g)].map((match) => match[1]);

    expect(states).toHaveLength(11);
    for (const state of states) {
      expect(i18n.exists(`admin.import.state.${state}`)).toBe(true);
    }
  });
});
