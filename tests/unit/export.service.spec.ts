import { ExportService } from '../../src/app/features/search/services/export.service';
import { SearchResult } from '../../src/app/features/search/models/search.models';

describe('ExportService', () => {
  let service: ExportService;

  beforeEach(() => {
    localStorage.clear();
    service = new ExportService();
  });

  it('exports csv rows and returns exported count', () => {
    const results: SearchResult[] = [
      {
        candidateId: '1',
        firstName: 'Ana',
        lastName: 'Perez',
        phone: '+34 600',
        email: 'ana@example.com',
        status: 'available',
        hasPrimaryCv: true,
        updatedAt: '2026-07-01T10:00:00Z',
      },
    ];

    const createSpy = vi.spyOn(URL, 'createObjectURL').mockReturnValue('blob:test');
    const revokeSpy = vi.spyOn(URL, 'revokeObjectURL').mockImplementation(() => undefined);
    const click = vi.fn();
    const createElementSpy = vi
      .spyOn(document, 'createElement')
      .mockReturnValue({ href: '', download: '', click } as unknown as HTMLAnchorElement);

    const count = service.exportCandidatesToCsv(results);

    expect(count).toBe(1);
    expect(createSpy).toHaveBeenCalled();
    expect(click).toHaveBeenCalled();
    expect(revokeSpy).toHaveBeenCalled();

    const batches = service.listBatches();
    expect(batches).toHaveLength(1);
    expect(batches[0].rowCount).toBe(1);
    expect(batches[0].fileName).toBe('candidatos.csv');

    createSpy.mockRestore();
    revokeSpy.mockRestore();
    createElementSpy.mockRestore();
  });
});
