import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter } from 'react-router';
import { services, type Services } from '../../src/app/core/di/services';
import { ServicesProvider } from '../../src/app/core/di/services-context';
import { signal } from '../../src/app/core/state/signal';
import { AppError } from '../../src/app/shared/models/error.models';
import { AdvancedSearchPage } from '../../src/app/features/search/pages/advanced-search-page';
import {
  EMPTY_SEARCH_FILTERS,
  type SearchResultPage,
} from '../../src/app/features/search/models/search.models';

/**
 * The page's own responsibilities: paging over the server's totals, and making a superseded
 * search stop rather than arrive late and overwrite the newer one.
 */
describe('AdvancedSearchPage', () => {
  const item = (id: string) => ({
    candidateId: id,
    firstName: 'Ana',
    lastName: id,
    phone: '+34 600 000 001',
    email: `${id}@ejemplo.test`,
    status: 'available' as const,
    hasPrimaryCv: true,
    primaryCvDocumentId: 'd-1',
    updatedAt: '2026-03-01T09:01:00Z',
  });

  const page = (ids: string[], overrides: Partial<SearchResultPage> = {}): SearchResultPage => ({
    items: ids.map(item),
    page: 1,
    pageSize: 25,
    totalCount: ids.length,
    ...overrides,
  });

  const toastService = { show: vi.fn() };
  const authService = {
    profile: signal<unknown>({ id: 'u-1', isActive: true }),
    hasPermission: vi.fn().mockReturnValue(true),
  };

  let searchPresetsService: {
    state: ReturnType<typeof signal<{ status: string; presets: unknown[] }>>;
    load: ReturnType<typeof vi.fn>;
    loadLastFilters: ReturnType<typeof vi.fn>;
    rememberLastFilters: ReturnType<typeof vi.fn>;
    emptyFilters: ReturnType<typeof vi.fn>;
    applyPreset: ReturnType<typeof vi.fn>;
    createPreset: ReturnType<typeof vi.fn>;
    updatePreset: ReturnType<typeof vi.fn>;
    removePreset: ReturnType<typeof vi.fn>;
  };

  beforeEach(() => {
    vi.clearAllMocks();
    searchPresetsService = {
      state: signal<{ status: string; presets: unknown[] }>({ status: 'loaded', presets: [] }),
      load: vi.fn().mockResolvedValue([]),
      loadLastFilters: vi.fn(() => structuredClone(EMPTY_SEARCH_FILTERS)),
      rememberLastFilters: vi.fn(),
      emptyFilters: vi.fn(() => structuredClone(EMPTY_SEARCH_FILTERS)),
      applyPreset: vi.fn(),
      createPreset: vi.fn(),
      updatePreset: vi.fn(),
      removePreset: vi.fn(),
    };
  });

  const renderPage = (candidateSearchService: unknown) =>
    render(
      <ServicesProvider
        value={
          {
            ...services,
            candidateSearchService,
            searchPresetsService,
            authService,
            toastService,
          } as unknown as Services
        }
      >
        <MemoryRouter>
          <AdvancedSearchPage />
        </MemoryRouter>
      </ServicesProvider>,
    );

  it('reports the server total rather than the number of rows on screen', async () => {
    const search = vi.fn().mockResolvedValue(page(['a', 'b'], { totalCount: 57, pageSize: 25 }));

    renderPage({ search, emptyFilters: () => structuredClone(EMPTY_SEARCH_FILTERS) });

    await waitFor(() =>
      expect(screen.getByTestId('search-total')).toHaveTextContent('57 candidatos encontrados'),
    );
    expect(screen.getByTestId('search-total')).toHaveTextContent('Página 1 de 3');
  });

  it('asks the server for the next page instead of slicing what it holds', async () => {
    const search = vi
      .fn()
      .mockResolvedValueOnce(page(['a'], { totalCount: 57, page: 1 }))
      .mockResolvedValueOnce(page(['b'], { totalCount: 57, page: 2 }));
    renderPage({ search, emptyFilters: () => structuredClone(EMPTY_SEARCH_FILTERS) });
    await waitFor(() => expect(screen.getByTestId('search-next-page')).toBeEnabled());

    await userEvent.click(screen.getByTestId('search-next-page'));

    await waitFor(() => expect(search).toHaveBeenCalledTimes(2));
    expect(search.mock.calls[1][1]).toMatchObject({ page: 2 });
  });

  it('aborts the in-flight request when a newer search supersedes it', async () => {
    const seen: AbortSignal[] = [];
    const search = vi.fn((_filters: unknown, options: { signal?: AbortSignal }) => {
      seen.push(options.signal!);
      return new Promise<SearchResultPage>(() => {});
    });
    renderPage({ search, emptyFilters: () => structuredClone(EMPTY_SEARCH_FILTERS) });
    await waitFor(() => expect(seen).toHaveLength(1));

    await userEvent.click(screen.getByRole('button', { name: 'Buscar' }));

    // Aborted at the network, not merely ignored on arrival: an ignored request still
    // occupies a connection and still reaches the server.
    await waitFor(() => expect(seen[0].aborted).toBe(true));
  });

  it('cannot apply the results of a superseded request', async () => {
    let resolveFirst: ((value: SearchResultPage) => void) | undefined;
    const search = vi
      .fn()
      .mockImplementationOnce(
        () =>
          new Promise<SearchResultPage>((resolve) => {
            resolveFirst = resolve;
          }),
      )
      .mockResolvedValue(page(['nuevo'], { totalCount: 1 }));
    renderPage({ search, emptyFilters: () => structuredClone(EMPTY_SEARCH_FILTERS) });
    await waitFor(() => expect(search).toHaveBeenCalledTimes(1));

    await userEvent.click(screen.getByRole('button', { name: 'Buscar' }));
    await waitFor(() =>
      expect(screen.getByTestId('search-total')).toHaveTextContent('1 candidato'),
    );
    // The abandoned request answers late, with a different count.
    resolveFirst?.(page(['viejo', 'viejo-2'], { totalCount: 999 }));

    await waitFor(() =>
      expect(screen.getByTestId('search-total')).toHaveTextContent('1 candidato encontrado'),
    );
    expect(screen.queryByText('999 candidatos encontrados')).toBeNull();
  });

  it('shows no error when a superseded request fails after cancellation', async () => {
    let rejectFirst: ((reason: unknown) => void) | undefined;
    const search = vi
      .fn()
      .mockImplementationOnce(
        () =>
          new Promise<SearchResultPage>((_resolve, reject) => {
            rejectFirst = reject;
          }),
      )
      .mockResolvedValue(page(['nuevo'], { totalCount: 1 }));
    renderPage({ search, emptyFilters: () => structuredClone(EMPTY_SEARCH_FILTERS) });
    await waitFor(() => expect(search).toHaveBeenCalledTimes(1));

    await userEvent.click(screen.getByRole('button', { name: 'Buscar' }));
    await waitFor(() =>
      expect(screen.getByTestId('search-total')).toHaveTextContent('1 candidato'),
    );
    rejectFirst?.(new AppError('CANCELLED'));

    // Cancelling on the user's behalf is not a failure the user needs to hear about.
    await waitFor(() =>
      expect(toastService.show).not.toHaveBeenCalledWith(expect.anything(), 'error'),
    );
    expect(screen.getByTestId('search-total')).toHaveTextContent('1 candidato encontrado');
  });

  it('reports a real failure without pretending the search returned nothing', async () => {
    const search = vi.fn().mockRejectedValue(new AppError('INTERNAL_ERROR', 'Servidor caído.'));

    renderPage({ search, emptyFilters: () => structuredClone(EMPTY_SEARCH_FILTERS) });

    await waitFor(() =>
      expect(screen.getByTestId('search-empty')).toHaveTextContent(
        'No se pudo completar la búsqueda.',
      ),
    );
    expect(toastService.show).toHaveBeenCalledWith('Servidor caído.', 'error');
  });
});
