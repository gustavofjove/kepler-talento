import { render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { ServicesProvider } from '../../src/app/core/di/services-context';
import { services, type Services } from '../../src/app/core/di/services';
import es from '../../src/assets/i18n/es.json';
import { ImportPage } from '../../src/app/features/admin/import/import-page';
import type { ImportRowReport } from '../../src/app/features/admin/import/import.models';
import { AppError } from '../../src/app/shared/models/error.models';
import { batch } from './support/import-doubles';

const copy = es as Record<string, string>;

const report = (overrides: Partial<ImportRowReport> = {}): ImportRowReport => ({
  batchId: 'b-1',
  phase: 'validation',
  items: [
    { rowNumber: 1, outcome: 'loaded', field: null, reasonCode: null },
    { rowNumber: 2, outcome: 'rejected', field: 'email', reasonCode: 'email.invalid' },
    { rowNumber: 3, outcome: 'skipped', field: 'email', reasonCode: 'candidate.duplicate' },
  ],
  page: 1,
  pageSize: 50,
  totalCount: 3,
  ...overrides,
});

function importServiceDouble(overrides: Record<string, unknown> = {}) {
  return {
    upload: vi.fn().mockResolvedValue(batch({ state: 'uploaded', rowCount: null, loadedRows: 0 })),
    getBatch: vi.fn(),
    validate: vi
      .fn()
      .mockResolvedValue(batch({ state: 'validating', rowCount: null, loadedRows: 0 })),
    commit: vi.fn().mockResolvedValue(batch({ state: 'committing' })),
    listBatches: vi.fn().mockResolvedValue({ items: [], page: 1, pageSize: 20, totalCount: 0 }),
    getRowReport: vi.fn().mockResolvedValue(report()),
    waitUntilSettled: vi.fn(),
    ...overrides,
  };
}

function renderPage(importService: ReturnType<typeof importServiceDouble>) {
  const toastService = { show: vi.fn() };
  const candidateService = { reload: vi.fn().mockResolvedValue(undefined) };
  render(
    <ServicesProvider
      value={
        {
          ...services,
          importService: importService as never,
          toastService: toastService as never,
          candidateService: candidateService as never,
        } as Services
      }
    >
      <ImportPage />
    </ServicesProvider>,
  );
  return { toastService, candidateService };
}

async function chooseFileAndValidate() {
  const file = new File(['first_name,last_name,email\n'], 'candidatos.csv', { type: 'text/csv' });
  await userEvent.upload(screen.getByLabelText(copy['admin.import.fileLabel']), file);
  await userEvent.click(screen.getByTestId('import-validate'));
}

describe('ImportPage', () => {
  it('uploads, waits for the scan, validates and shows the dry-run report without committing', async () => {
    const service = importServiceDouble({
      waitUntilSettled: vi
        .fn()
        .mockResolvedValueOnce(batch({ state: 'scanned', rowCount: null, loadedRows: 0 }))
        .mockResolvedValueOnce(
          batch({
            state: 'validated',
            rowCount: 3,
            loadedRows: 1,
            rejectedRows: 1,
            skippedRows: 1,
          }),
        ),
    });
    renderPage(service);

    await chooseFileAndValidate();

    expect(await screen.findByTestId('import-row-report')).toBeInTheDocument();
    expect(service.validate).toHaveBeenCalledOnce();
    expect(service.commit).not.toHaveBeenCalled();
    expect(screen.getByTestId('import-state')).toHaveAttribute('data-state', 'validated');
    expect(screen.getByTestId('import-count-rejected')).toHaveTextContent('1');
    const rows = within(screen.getByTestId('import-row-report')).getAllByRole('row');
    expect(rows[2]).toHaveTextContent(copy['admin.import.reason.email.invalid']);
    expect(rows[3]).toHaveTextContent(copy['admin.import.reason.candidate.duplicate']);
    // Rejected rows keep the commit shut, and the page says what to do instead.
    expect(screen.getByTestId('import-commit')).toBeDisabled();
    expect(screen.getByText(copy['admin.import.fixAndRetry'])).toBeInTheDocument();
  });

  it('commits a clean validated batch and reports what was loaded', async () => {
    const service = importServiceDouble({
      waitUntilSettled: vi
        .fn()
        .mockResolvedValueOnce(batch({ state: 'scanned' }))
        .mockResolvedValueOnce(batch({ state: 'validated', rowCount: 2, loadedRows: 2 }))
        .mockResolvedValueOnce(
          batch({
            state: 'committed',
            rowCount: 2,
            loadedRows: 2,
            committedAt: '2026-09-16T11:00:00Z',
          }),
        ),
      getRowReport: vi.fn().mockResolvedValue(report({ items: [], totalCount: 0 })),
    });
    const { toastService, candidateService } = renderPage(service);

    await chooseFileAndValidate();
    await waitFor(() => expect(screen.getByTestId('import-commit')).toBeEnabled());
    await userEvent.click(screen.getByTestId('import-commit'));

    await waitFor(() =>
      expect(screen.getByTestId('import-state')).toHaveAttribute('data-state', 'committed'),
    );
    expect(service.commit).toHaveBeenCalledOnce();
    // The session's candidate cache is refreshed so Candidatos shows the imported people.
    expect(candidateService.reload).toHaveBeenCalledOnce();
    expect(toastService.show).toHaveBeenCalledWith(
      'Carga confirmada: 2 candidatos creados.',
      'success',
    );
    expect(screen.getByText(copy['admin.import.loaded'])).toBeInTheDocument();
  });

  it('shows a scan-blocked batch as refused and never offers to validate or commit it', async () => {
    const service = importServiceDouble({
      waitUntilSettled: vi.fn().mockResolvedValue(
        batch({
          state: 'infected',
          rowCount: null,
          loadedRows: 0,
          failureCode: 'import.scan.infected',
        }),
      ),
    });
    renderPage(service);

    await chooseFileAndValidate();

    expect(await screen.findByTestId('import-refused')).toHaveTextContent(
      copy['admin.import.refusedExplanation.infected'],
    );
    expect(service.validate).not.toHaveBeenCalled();
    expect(screen.getByTestId('import-commit')).toBeDisabled();
  });

  it('explains a structural refusal with the column it names', async () => {
    const service = importServiceDouble({
      waitUntilSettled: vi
        .fn()
        .mockResolvedValueOnce(batch({ state: 'scanned' }))
        .mockResolvedValueOnce(
          batch({
            state: 'failed',
            rowCount: null,
            loadedRows: 0,
            failureCode: 'import.column.missing',
            failureDetail: 'email',
          }),
        ),
    });
    renderPage(service);

    await chooseFileAndValidate();

    expect(await screen.findByTestId('import-failure')).toHaveTextContent(
      'Falta la columna obligatoria email.',
    );
  });

  it('says when it stopped polling instead of appearing to hang', async () => {
    const { TranslatableError } = await import('../../src/app/core/i18n/translatable-error');
    const service = importServiceDouble({
      waitUntilSettled: vi
        .fn()
        .mockRejectedValue(new TranslatableError('admin.import.errors.pollTimeout')),
    });
    const { toastService } = renderPage(service);

    await chooseFileAndValidate();

    expect(await screen.findByTestId('import-stalled')).toHaveTextContent(
      copy['admin.import.stalled'],
    );
    expect(toastService.show).toHaveBeenCalledWith(
      copy['admin.import.errors.pollTimeout'],
      'error',
    );
  });

  it.each([
    ['import.batch.not_ready'],
    ['import.batch.refused'],
    ['import.batch.not_validated'],
    ['import.batch.has_rejected_rows'],
    ['import.batch.expired'],
    ['import.batch.version_conflict'],
    ['import.file.type_not_allowed'],
    ['import.file.too_large'],
  ])('shows its own Spanish sentence for the %s refusal', async (code) => {
    const service = importServiceDouble({
      upload: vi
        .fn()
        .mockRejectedValue(new AppError('CONFLICT', 'server text', undefined, undefined, code)),
    });
    const { toastService } = renderPage(service);

    await chooseFileAndValidate();

    await waitFor(() =>
      expect(toastService.show).toHaveBeenCalledWith(copy[`admin.import.errors.${code}`], 'error'),
    );
  });

  it('lists the server batch history and reopens a batch with its report', async () => {
    const earlier = batch({
      id: 'b-9',
      state: 'committed',
      originalFileName: null,
      uploadedByCaller: false,
      loadedRows: 4,
      rowCount: 4,
      committedAt: '2026-09-15T09:00:00Z',
    });
    const service = importServiceDouble({
      listBatches: vi
        .fn()
        .mockResolvedValue({ items: [earlier], page: 1, pageSize: 20, totalCount: 1 }),
      getRowReport: vi.fn().mockResolvedValue(report({ batchId: 'b-9', phase: 'commit' })),
    });
    renderPage(service);

    const history = await screen.findByTestId('import-history');
    expect(within(history).getByText(copy['admin.import.otherUploader'])).toBeInTheDocument();
    await userEvent.click(screen.getByTestId('import-history-open-b-9'));

    expect(await screen.findByText(copy['admin.import.reportCommit'])).toBeInTheDocument();
    expect(service.getRowReport).toHaveBeenCalledWith('b-9', 1, 50);
  });

  it('warns about an identical file imported before and about a purged file', async () => {
    const service = importServiceDouble({
      listBatches: vi.fn().mockResolvedValue({
        items: [
          batch({
            id: 'b-2',
            state: 'expired',
            fileRetained: false,
            sameFileCommittedAt: '2026-09-01T09:00:00Z',
          }),
        ],
        page: 1,
        pageSize: 20,
        totalCount: 1,
      }),
    });
    renderPage(service);

    await userEvent.click(await screen.findByTestId('import-history-open-b-2'));

    expect(await screen.findByTestId('import-file-purged')).toBeInTheDocument();
    expect(screen.getByTestId('import-same-file')).toBeInTheDocument();
    expect(screen.getByTestId('import-commit')).toBeDisabled();
  });

  it('keeps the controls the end-to-end suite binds to', async () => {
    const service = importServiceDouble();
    renderPage(service);
    await waitFor(() => expect(service.listBatches).toHaveBeenCalled());

    expect(screen.getByTestId('import-file')).toHaveAttribute('name', 'file');
    expect(screen.getByTestId('import-validate')).toBeDisabled();
  });
});
