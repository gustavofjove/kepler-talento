import { ImportService } from '../../src/app/features/admin/import/import.service';

describe('ImportService', () => {
  let service: ImportService;

  beforeEach(() => {
    localStorage.clear();
    service = new ImportService();
  });

  it('validates a correct csv payload without row errors', () => {
    const summary = service.validateCsvContent(
      'candidates.csv',
      'first_name,last_name,email\nAna,Perez,ana@example.com',
      true,
    );

    expect(summary.totalRows).toBe(1);
    expect(summary.errorRows).toBe(0);
    expect(summary.errors).toHaveLength(0);
    expect(summary.batchId).toBeTruthy();
  });

  it('reports missing mandatory columns', () => {
    const summary = service.validateCsvContent(
      'candidates.csv',
      'first_name,email\nAna,ana@example.com',
      true,
    );

    expect(summary.errors.some((error) => error.field === 'last_name')).toBe(true);
  });

  it('reports row level validation errors', () => {
    const summary = service.validateCsvContent(
      'candidates.csv',
      'first_name,last_name,email\nAna,,ana@example.com\nBea,Santos,bad-email',
      false,
    );

    expect(summary.errorRows).toBe(2);
    expect(summary.loadedRows).toBe(0);
  });

  it('stores import batches and allows committing a dry-run batch', () => {
    const dryRun = service.validateCsvContent(
      'batch.csv',
      'first_name,last_name,email\nAna,Perez,ana@example.com',
      true,
    );

    expect(dryRun.batchId).toBeTruthy();
    const created = service.listBatches();
    expect(created).toHaveLength(1);
    expect(created[0].status).toBe('validated');

    const committed = service.markCommitted(dryRun.batchId as string);
    expect(committed.status).toBe('committed');
    expect(committed.committedAt).toBeTruthy();

    const updated = service.listBatches();
    expect(updated[0].status).toBe('committed');
    expect(updated[0].dryRun).toBe(false);
  });
});
