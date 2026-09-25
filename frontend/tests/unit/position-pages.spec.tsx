import { act, fireEvent, render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter, Route, Routes, useLocation } from 'react-router';
import { services, type Services } from '../../src/app/core/di/services';
import { ServicesProvider } from '../../src/app/core/di/services-context';
import { RequirePermission } from '../../src/app/core/routing/require-permission';
import { signal } from '../../src/app/core/state/signal';
import { PositionDetailPage } from '../../src/app/features/positions/position-detail-page';
import { PositionFormPage } from '../../src/app/features/positions/position-form-page';
import { PositionListPage } from '../../src/app/features/positions/position-list-page';
import type {
  Position,
  PositionCandidate,
  PositionPage,
} from '../../src/app/features/positions/position.models';
import {
  EMPTY_SEARCH_FILTERS,
  type SearchFilters,
  type SearchResult,
  type SearchResultPage,
} from '../../src/app/features/search/models/search.models';
import { AppError } from '../../src/app/shared/models/error.models';
import type { Permission } from '../../src/app/shared/models/auth.models';

const ALL: Permission[] = [
  'positions.read',
  'positions.manage',
  'candidates.read',
  'presets.manage',
  'documents.download',
];

const stored: Position = {
  id: 'pos-1',
  title: 'Programador sénior',
  description: '<p>Hola <strong>equipo</strong></p><ul><li>Uno</li></ul>',
  location: 'Madrid',
  status: 'open',
  requirements: { ...structuredClone(EMPTY_SEARCH_FILTERS), text: 'java' },
  createdAtUtc: '2026-09-01T09:00:00Z',
  updatedAtUtc: '2026-09-02T09:00:00Z',
  version: 4,
};

const emptyPage: PositionPage = { items: [], page: 1, pageSize: 25, totalCount: 0 };
const noResults: SearchResultPage = { items: [], page: 1, pageSize: 25, totalCount: 0 };

function LocationProbe() {
  return <p data-testid="location">{useLocation().pathname}</p>;
}

function deferred<T>() {
  let resolve!: (value: T) => void;
  const promise = new Promise<T>((done) => (resolve = done));
  return { promise, resolve };
}

describe('Position pages', () => {
  let granted: Permission[];
  let positionService: {
    state: ReturnType<typeof signal<PositionPage>>;
    list: ReturnType<typeof vi.fn>;
    get: ReturnType<typeof vi.fn>;
    create: ReturnType<typeof vi.fn>;
    update: ReturnType<typeof vi.fn>;
    listCandidates: ReturnType<typeof vi.fn>;
    addCandidate: ReturnType<typeof vi.fn>;
    changeStage: ReturnType<typeof vi.fn>;
    removeCandidate: ReturnType<typeof vi.fn>;
  };
  let candidateSearchService: {
    search: ReturnType<typeof vi.fn>;
    emptyFilters: () => SearchFilters;
  };
  let searchPresetsService: {
    state: ReturnType<typeof signal<unknown>>;
    load: ReturnType<typeof vi.fn>;
    applyPreset: ReturnType<typeof vi.fn>;
    createPreset: ReturnType<typeof vi.fn>;
  };
  const toastService = { show: vi.fn() };
  const confirmDialogService = { confirm: vi.fn().mockResolvedValue(true) };

  beforeEach(() => {
    vi.clearAllMocks();
    granted = [...ALL];
    const state = signal<PositionPage>(emptyPage);
    positionService = {
      state,
      list: vi.fn(async () => {
        const page = {
          items: [{ ...stored, candidateCount: 3 }],
          page: 1,
          pageSize: 25,
          totalCount: 1,
        } satisfies PositionPage;
        state.set(page);
        return page;
      }),
      get: vi.fn().mockResolvedValue(structuredClone(stored)),
      create: vi.fn().mockResolvedValue({ ...stored, id: 'pos-new' }),
      update: vi.fn().mockResolvedValue(stored),
      listCandidates: vi.fn().mockResolvedValue([]),
      addCandidate: vi.fn(),
      changeStage: vi.fn(),
      removeCandidate: vi.fn().mockResolvedValue(undefined),
    };
    candidateSearchService = {
      search: vi.fn().mockResolvedValue(noResults),
      emptyFilters: () => structuredClone(EMPTY_SEARCH_FILTERS),
    };
    searchPresetsService = {
      state: signal<unknown>({
        status: 'loaded',
        presets: [{ id: 'pre-1', name: 'Java Madrid', filters: EMPTY_SEARCH_FILTERS }],
      }),
      load: vi.fn().mockResolvedValue([]),
      applyPreset: vi.fn(),
      createPreset: vi.fn().mockResolvedValue({ id: 'pre-2' }),
    };
  });

  const renderAt = (path: string) => {
    const profile = signal<unknown>({ id: 'u-1', isActive: true });
    return render(
      <ServicesProvider
        value={
          {
            ...services,
            positionService,
            candidateSearchService,
            searchPresetsService,
            toastService,
            confirmDialogService,
            authService: {
              profile,
              hasPermission: (permission: Permission) => granted.includes(permission),
            },
          } as unknown as Services
        }
      >
        <MemoryRouter initialEntries={[path]}>
          <LocationProbe />
          <Routes>
            <Route element={<RequirePermission permission="positions.read" />}>
              <Route path="/app/positions" element={<PositionListPage />} />
              <Route path="/app/positions/:id" element={<PositionDetailPage />} />
            </Route>
            <Route element={<RequirePermission permission="positions.manage" />}>
              <Route path="/app/positions/new" element={<PositionFormPage />} />
              <Route path="/app/positions/:id/edit" element={<PositionFormPage />} />
            </Route>
            <Route path="*" element={null} />
          </Routes>
        </MemoryRouter>
      </ServicesProvider>,
    );
  };

  const location = () => screen.getByTestId('location');

  // ---- routes ----

  it.each([
    ['/app/positions', 'positions.read'],
    ['/app/positions/pos-1', 'positions.read'],
    ['/app/positions/new', 'positions.manage'],
    ['/app/positions/pos-1/edit', 'positions.manage'],
  ] as const)('guards %s behind %s', async (path, permission) => {
    granted = ALL.filter((value) => value !== permission);
    renderAt(path);

    await waitFor(() => expect(location()).not.toHaveTextContent(path));
    expect(positionService.get).not.toHaveBeenCalled();
    expect(positionService.list).not.toHaveBeenCalled();
  });

  // ---- list ----

  describe('list', () => {
    beforeEach(() => vi.useFakeTimers({ shouldAdvanceTime: true }));
    afterEach(() => vi.useRealTimers());

    it('loads open positions sorted by update and renders them from the service signal', async () => {
      renderAt('/app/positions');

      expect(screen.getByRole('status')).toBeInTheDocument();
      expect(await screen.findByRole('link', { name: stored.title })).toHaveAttribute(
        'href',
        '/app/positions/pos-1',
      );
      expect(screen.getByTestId('position-candidate-count')).toHaveTextContent('3');
      expect(positionService.list).toHaveBeenLastCalledWith(
        expect.objectContaining({
          status: 'open',
          text: '',
          sortField: 'updatedAt',
          sortDirection: 'desc',
          page: 1,
        }),
      );
    });

    it('debounces text, applies status and sort, and resets to the first page', async () => {
      renderAt('/app/positions');
      await screen.findByRole('link', { name: stored.title });
      const user = userEvent.setup({ advanceTimers: vi.advanceTimersByTime });

      await user.type(screen.getByTestId('position-text-filter'), 'mad');
      await user.selectOptions(screen.getByRole('combobox', { name: 'Estado' }), 'all');
      await user.selectOptions(screen.getByRole('combobox', { name: 'Ordenar por' }), 'title');
      await act(() => vi.advanceTimersByTimeAsync(300));

      const calls = positionService.list.mock.calls.map(([query]) => query as { text: string });
      // One request for the settled input, not one per keystroke.
      expect(calls.filter((query) => query.text === 'm' || query.text === 'ma')).toHaveLength(0);
      expect(positionService.list).toHaveBeenLastCalledWith(
        expect.objectContaining({
          text: 'mad',
          status: 'all',
          sortField: 'title',
          sortDirection: 'asc',
          page: 1,
        }),
      );
    });

    it('opens the position when a plain part of its row is clicked (KTL-31)', async () => {
      renderAt('/app/positions');
      const row = (await screen.findAllByTestId('position-row'))[0];

      fireEvent.click(within(row).getByText('Madrid'));

      await waitFor(() => expect(location()).toHaveTextContent('/app/positions/pos-1'));
    });

    it('opens the position in a new tab on Ctrl-click and middle-click (KTL-31)', async () => {
      const open = vi.spyOn(window, 'open').mockReturnValue(null);
      renderAt('/app/positions');
      const cell = within((await screen.findAllByTestId('position-row'))[0]).getByText('Madrid');

      fireEvent.click(cell, { ctrlKey: true });
      fireEvent(cell, new MouseEvent('auxclick', { bubbles: true, button: 1 }));

      expect(open).toHaveBeenCalledTimes(2);
      expect(open).toHaveBeenCalledWith('/app/positions/pos-1', '_blank', 'noopener');
      expect(location()).toHaveTextContent(/^\/app\/positions$/);
      open.mockRestore();
    });

    it('shows the empty state and hides creation from a reader', async () => {
      granted = ['positions.read'];
      positionService.list.mockResolvedValue(emptyPage);
      renderAt('/app/positions');

      expect(
        await screen.findByText('No hay posiciones para los filtros seleccionados.'),
      ).toBeVisible();
      expect(screen.queryByTestId('position-create')).not.toBeInTheDocument();
    });

    it('offers creation to a manager', async () => {
      renderAt('/app/positions');
      expect(await screen.findByTestId('position-create')).toHaveAttribute(
        'href',
        '/app/positions/new',
      );
    });

    it('renders no breadcrumb, being a top-level destination (KTL-23)', async () => {
      renderAt('/app/positions');

      await screen.findByRole('link', { name: stored.title });
      expect(screen.queryByTestId('breadcrumb')).not.toBeInTheDocument();
    });

    it('reports a failed load', async () => {
      positionService.list.mockRejectedValue(new Error(''));
      renderAt('/app/positions');

      await waitFor(() =>
        expect(toastService.show).toHaveBeenCalledWith(
          'No se pudieron cargar las posiciones.',
          'error',
        ),
      );
    });
  });

  // ---- form ----

  describe('form', () => {
    it('uses the shared CV and status controls for position requirements', async () => {
      renderAt('/app/positions/new');
      expect(screen.getByRole('checkbox', { name: 'Con CV' })).toBeChecked();
      expect(screen.getByRole('checkbox', { name: 'Sin CV' })).toBeChecked();
      await userEvent.click(screen.getByTestId('status-disclosure'));
      expect(screen.getByRole('checkbox', { name: 'Disponible' })).toBeChecked();
      await userEvent.click(screen.getByRole('checkbox', { name: 'Nuevo' }));
      expect(screen.getByRole('checkbox', { name: 'Nuevo' })).not.toBeChecked();
      expect(positionService.create).not.toHaveBeenCalled();
    });

    it('creates a position and opens its detail page', async () => {
      renderAt('/app/positions/new');

      await userEvent.type(screen.getByTestId('position-title'), 'Analista QA');
      await userEvent.type(screen.getByTestId('position-location'), 'Bilbao');
      await userEvent.click(screen.getByRole('button', { name: 'Guardar' }));

      await waitFor(() => expect(location()).toHaveTextContent('/app/positions/pos-new'));
      expect(positionService.create).toHaveBeenCalledWith(
        expect.objectContaining({ title: 'Analista QA', location: 'Bilbao' }),
      );
      expect(screen.queryByTestId('position-status')).not.toBeInTheDocument();
    });

    it('cancels without writing anything', async () => {
      renderAt('/app/positions/pos-1/edit');
      await waitFor(() => expect(screen.getByTestId('position-title')).toHaveValue(stored.title));

      await userEvent.type(screen.getByTestId('position-title'), ' cambiado');
      await userEvent.click(screen.getByRole('button', { name: 'Cancelar' }));

      expect(location()).toHaveTextContent('/app/positions/pos-1');
      expect(positionService.update).not.toHaveBeenCalled();
      expect(positionService.create).not.toHaveBeenCalled();
    });

    it('closes a position against the version it loaded', async () => {
      renderAt('/app/positions/pos-1/edit');
      await waitFor(() => expect(screen.getByTestId('position-title')).toHaveValue(stored.title));

      await userEvent.selectOptions(screen.getByTestId('position-status'), 'closed');
      await userEvent.click(screen.getByRole('button', { name: 'Guardar' }));

      await waitFor(() =>
        expect(positionService.update).toHaveBeenCalledWith(
          'pos-1',
          expect.objectContaining({ status: 'closed', requirements: stored.requirements }),
          4,
        ),
      );
    });

    it('reopens a closed position', async () => {
      positionService.get.mockResolvedValue({ ...structuredClone(stored), status: 'closed' });
      renderAt('/app/positions/pos-1/edit');
      await waitFor(() => expect(screen.getByTestId('position-status')).toHaveValue('closed'));

      await userEvent.selectOptions(screen.getByTestId('position-status'), 'open');
      await userEvent.click(screen.getByRole('button', { name: 'Guardar' }));

      await waitFor(() =>
        expect(positionService.update).toHaveBeenCalledWith(
          'pos-1',
          expect.objectContaining({ status: 'open' }),
          4,
        ),
      );
    });

    it.each([
      ['position.title.conflict', 'Ya existe una posición con ese título.'],
      ['position.concurrency.conflict', 'La posición ha cambiado. Vuelva a cargarla.'],
    ])('explains a %s refusal and keeps the draft', async (code, message) => {
      positionService.update.mockRejectedValue(
        new AppError('CONFLICT', message, undefined, undefined, code),
      );
      renderAt('/app/positions/pos-1/edit');
      await waitFor(() => expect(screen.getByTestId('position-title')).toHaveValue(stored.title));

      await userEvent.type(screen.getByTestId('position-title'), ' B');
      await userEvent.click(screen.getByRole('button', { name: 'Guardar' }));

      await waitFor(() => expect(toastService.show).toHaveBeenCalledWith(message, 'error'));
      expect(screen.getByTestId('position-title')).toHaveValue(`${stored.title} B`);
      expect(location()).toHaveTextContent('/app/positions/pos-1/edit');
    });

    it('applies a copy of a preset that later changes do not reach', async () => {
      const presetFilters = { ...structuredClone(EMPTY_SEARCH_FILTERS), text: 'kotlin' };
      searchPresetsService.applyPreset.mockResolvedValue(presetFilters);
      renderAt('/app/positions/new');

      await userEvent.selectOptions(screen.getByTestId('position-preset'), 'pre-1');
      await userEvent.click(screen.getByRole('button', { name: 'Aplicar copia' }));
      await waitFor(() =>
        expect(screen.getByRole('textbox', { name: 'Texto' })).toHaveValue('kotlin'),
      );

      // Mutating the preset's own value afterwards must not change the draft.
      presetFilters.text = 'cobol';
      await userEvent.type(screen.getByTestId('position-title'), 'Kotlin');
      await userEvent.click(screen.getByRole('button', { name: 'Guardar' }));

      await waitFor(() => expect(positionService.create).toHaveBeenCalled());
      const draft = positionService.create.mock.calls[0][0] as { requirements: SearchFilters };
      expect(draft.requirements).toMatchObject({ text: 'kotlin' });
      expect(JSON.stringify(draft)).not.toContain('pre-1');
    });

    it('keeps the draft when the selected preset no longer exists', async () => {
      searchPresetsService.applyPreset.mockRejectedValue(
        new AppError('NOT_FOUND', 'No se pudo aplicar el preset; el borrador no ha cambiado.'),
      );
      renderAt('/app/positions/pos-1/edit');
      await waitFor(() =>
        expect(screen.getByRole('textbox', { name: 'Texto' })).toHaveValue('java'),
      );

      await userEvent.selectOptions(screen.getByTestId('position-preset'), 'pre-1');
      await userEvent.click(screen.getByRole('button', { name: 'Aplicar copia' }));

      await waitFor(() =>
        expect(toastService.show).toHaveBeenCalledWith(expect.any(String), 'error'),
      );
      expect(screen.getByRole('textbox', { name: 'Texto' })).toHaveValue('java');
    });

    it('saves the draft requirements as a new preset without linking it', async () => {
      renderAt('/app/positions/pos-1/edit');
      await waitFor(() =>
        expect(screen.getByRole('textbox', { name: 'Texto' })).toHaveValue('java'),
      );

      await userEvent.type(
        screen.getByRole('textbox', { name: 'Nombre del nuevo preset' }),
        'Copia Java',
      );
      await userEvent.click(screen.getByRole('button', { name: 'Guardar como preset' }));

      await waitFor(() =>
        expect(searchPresetsService.createPreset).toHaveBeenCalledWith(
          'Copia Java',
          expect.objectContaining({ text: 'java' }),
        ),
      );
      expect(searchPresetsService.createPreset.mock.calls[0][1]).not.toBe(stored.requirements);
    });

    it('lets a searcher apply presets without offering preset management', async () => {
      granted = ['positions.read', 'positions.manage', 'candidates.read'];
      renderAt('/app/positions/new');

      expect(screen.getByTestId('position-preset')).toBeInTheDocument();
      expect(screen.queryByRole('button', { name: 'Guardar como preset' })).not.toBeInTheDocument();
    });

    it('offers no preset controls without candidate or preset permissions', () => {
      granted = ['positions.read', 'positions.manage'];
      searchPresetsService.state.set({ status: 'idle', presets: [] });
      renderAt('/app/positions/new');

      expect(screen.queryByTestId('position-preset')).not.toBeInTheDocument();
      expect(screen.queryByRole('button', { name: 'Guardar como preset' })).not.toBeInTheDocument();
      expect(searchPresetsService.load).not.toHaveBeenCalled();
    });

    it('labels every field and keeps stable names', () => {
      renderAt('/app/positions/new');

      expect(screen.getByRole('textbox', { name: 'Título' })).toHaveAttribute('name', 'title');
      expect(screen.getByRole('textbox', { name: 'Ubicación' })).toHaveAttribute(
        'name',
        'location',
      );
      expect(screen.getByRole('textbox', { name: 'Descripción' })).toHaveAttribute(
        'name',
        'description',
      );
    });
  });

  // ---- breadcrumb (KTL-23) ----

  describe('breadcrumb', () => {
    const trail = () => within(screen.getByTestId('breadcrumb'));
    const trailText = () =>
      trail()
        .getAllByRole('listitem')
        .map((item) => item.textContent);

    it('leads from a position back to the list', async () => {
      renderAt('/app/positions/pos-1');
      await screen.findByRole('heading', { level: 1, name: stored.title });

      expect(trailText()).toEqual(['Posiciones', stored.title]);
      expect(trail().getByRole('link', { name: 'Posiciones' })).toHaveAttribute(
        'href',
        '/app/positions',
      );
      expect(trail().getByText(stored.title)).toHaveAttribute('aria-current', 'page');
    });

    it('offers the list while the position loads', () => {
      positionService.get.mockReturnValue(new Promise(() => {}));
      renderAt('/app/positions/pos-1');

      expect(trailText()).toEqual(['Posiciones']);
      expect(trail().getByRole('link', { name: 'Posiciones' })).toBeInTheDocument();
    });

    it('leads from the edit form to the position and the list, keeping the stored title', async () => {
      renderAt('/app/positions/pos-1/edit');
      await waitFor(() => expect(screen.getByTestId('position-title')).toHaveValue(stored.title));

      await userEvent.type(screen.getByTestId('position-title'), ' cambiado');

      expect(trailText()).toEqual(['Posiciones', stored.title, 'Editar']);
      expect(trail().getByRole('link', { name: stored.title })).toHaveAttribute(
        'href',
        '/app/positions/pos-1',
      );
      expect(trail().getByText('Editar')).toHaveAttribute('aria-current', 'page');
      // The breadcrumb is an extra way back, not a replacement for «Cancelar».
      expect(screen.getByRole('button', { name: 'Cancelar' })).toBeInTheDocument();
    });

    it('reads «Nueva posición» when creating', () => {
      renderAt('/app/positions/new');

      expect(trailText()).toEqual(['Posiciones', 'Nueva posición']);
      expect(trail().getByText('Nueva posición')).toHaveAttribute('aria-current', 'page');
    });

    it('renders segments as text for a manager who may not read positions', async () => {
      granted = ALL.filter((value) => value !== 'positions.read');
      renderAt('/app/positions/pos-1/edit');
      await waitFor(() => expect(screen.getByTestId('position-title')).toHaveValue(stored.title));

      expect(trailText()).toEqual(['Posiciones', stored.title, 'Editar']);
      expect(trail().queryByRole('link')).not.toBeInTheDocument();
    });
  });

  // ---- detail ----

  describe('detail', () => {
    it('evaluates the stored requirements live and shows the canonical description', async () => {
      renderAt('/app/positions/pos-1');

      expect(await screen.findByRole('heading', { level: 1, name: stored.title })).toBeVisible();
      await waitFor(() =>
        expect(candidateSearchService.search).toHaveBeenCalledWith(
          stored.requirements,
          expect.objectContaining({ page: 1 }),
        ),
      );
      const description = screen.getByTestId('position-description');
      expect(within(description).getByRole('list')).toBeInTheDocument();
      expect(within(description).queryByRole('heading')).not.toBeInTheDocument();
      expect(screen.getByRole('link', { name: 'Editar' })).toHaveAttribute(
        'href',
        '/app/positions/pos-1/edit',
      );
    });

    it('still searches for a closed position', async () => {
      positionService.get.mockResolvedValue({ ...structuredClone(stored), status: 'closed' });
      renderAt('/app/positions/pos-1');

      await screen.findByRole('heading', { level: 1, name: stored.title });
      await waitFor(() => expect(candidateSearchService.search).toHaveBeenCalledTimes(1));
    });

    it('makes no candidate request and explains why without candidates.read', async () => {
      granted = ['positions.read'];
      renderAt('/app/positions/pos-1');

      expect(await screen.findByText('No tienes permiso para consultar candidatos.')).toBeVisible();
      expect(candidateSearchService.search).not.toHaveBeenCalled();
      expect(screen.queryByTestId('search-total')).not.toBeInTheDocument();
      expect(screen.queryByRole('link', { name: 'Editar' })).not.toBeInTheDocument();
    });

    it('pages through matches and aborts the search in flight when leaving', async () => {
      const second = deferred<SearchResultPage>();
      candidateSearchService.search
        .mockResolvedValueOnce({ ...noResults, totalCount: 60 })
        .mockReturnValueOnce(second.promise);
      const view = renderAt('/app/positions/pos-1');
      await waitFor(() => expect(screen.getByTestId('search-next-page')).toBeEnabled());

      await userEvent.click(screen.getByTestId('search-next-page'));
      const signalOfPageTwo = candidateSearchService.search.mock.calls[1][1].signal as AbortSignal;
      expect(candidateSearchService.search.mock.calls[1][1]).toMatchObject({ page: 2 });
      view.unmount();

      expect(signalOfPageTwo.aborted).toBe(true);
    });

    it('ignores a response that arrives after a newer search started', async () => {
      const first = deferred<SearchResultPage>();
      candidateSearchService.search.mockReturnValueOnce(first.promise);
      const view = renderAt('/app/positions/pos-1');
      await waitFor(() => expect(candidateSearchService.search).toHaveBeenCalledTimes(1));
      const firstSignal = candidateSearchService.search.mock.calls[0][1].signal as AbortSignal;

      view.unmount();
      first.resolve({ ...noResults, totalCount: 99 });

      expect(firstSignal.aborted).toBe(true);
    });
  });

  // ---- KTL-30: position candidates ----

  describe('position candidates', () => {
    const link = (overrides: Partial<PositionCandidate> = {}): PositionCandidate => ({
      candidateId: 'c-1',
      firstName: 'Ana',
      lastName: 'García',
      email: 'ana@example.test',
      phone: '600111222',
      hasPrimaryCv: true,
      candidateIsActive: true,
      stage: 'new',
      addedAtUtc: '2026-09-20T09:00:00Z',
      updatedAtUtc: '2026-09-20T09:00:00Z',
      version: 3,
      ...overrides,
    });
    const match = (candidateId: string, firstName: string): SearchResult => ({
      candidateId,
      firstName,
      lastName: 'Pérez',
      phone: '',
      email: `${firstName.toLowerCase()}@example.test`,
      status: 'available',
      hasPrimaryCv: false,
      updatedAt: '2026-09-20T09:00:00Z',
      isActive: true,
    });
    const panel = () => within(screen.getByTestId('position-candidates'));

    it('lists the links above the matches with a removed marker', async () => {
      positionService.listCandidates.mockResolvedValue([
        link(),
        link({
          candidateId: 'c-2',
          firstName: 'Luis',
          candidateIsActive: false,
          stage: 'rejected',
        }),
      ]);
      renderAt('/app/positions/pos-1');

      const rows = await within(await screen.findByTestId('position-candidates')).findAllByTestId(
        'position-candidate-row',
      );
      expect(rows).toHaveLength(2);
      expect(within(rows[0]).getByRole('link', { name: 'Ana García' })).toHaveAttribute(
        'href',
        '/app/candidates/c-1',
      );
      expect(within(rows[1]).getByTestId('position-candidate-inactive')).toHaveTextContent(
        'Eliminado',
      );
      expect(within(rows[0]).getByRole('combobox', { name: 'Estado de Ana García' })).toHaveValue(
        'new',
      );
      const headings = screen.getAllByRole('heading', { level: 2 }).map((h) => h.textContent);
      expect(headings.indexOf('Candidatos de la posición')).toBeLessThan(
        headings.indexOf('Candidatos que encajan'),
      );
    });

    it('shows the search columns with the stage and date added, and opens the candidate from the row', async () => {
      positionService.listCandidates.mockResolvedValue([link()]);
      renderAt('/app/positions/pos-1');

      await screen.findByTestId('position-candidates');
      const row = (await panel().findAllByTestId('position-candidate-row'))[0];
      expect(
        within(screen.getByTestId('position-candidates'))
          .getAllByRole('columnheader')
          .map((header) => header.textContent),
      ).toEqual(['Candidato', 'Teléfono', 'Estado en la posición', 'CV', 'Añadido', 'Acciones']);
      expect(within(row).getByRole('link', { name: 'ana@example.test' })).toHaveAttribute(
        'href',
        'mailto:ana@example.test',
      );
      expect(within(row).getByText('600111222').closest('a')).toBeNull();
      expect(within(row).getByText('Disponible')).toBeVisible();
      expect(within(row).queryByRole('link', { name: /^Ver/ })).toBeNull();

      await userEvent.click(within(row).getByText('600111222'));

      expect(location()).toHaveTextContent('/app/candidates/c-1');
    });

    it('adds a match and marks already linked matches as added', async () => {
      positionService.listCandidates.mockResolvedValue([link()]);
      candidateSearchService.search.mockResolvedValue({
        ...noResults,
        items: [match('c-1', 'Ana'), match('c-9', 'Eva')],
        totalCount: 2,
      });
      positionService.addCandidate.mockResolvedValue(
        link({ candidateId: 'c-9', firstName: 'Eva', lastName: 'Pérez' }),
      );
      renderAt('/app/positions/pos-1');

      const add = await screen.findByRole('button', { name: 'Añadir a Eva Pérez a la posición' });
      expect(screen.getAllByTestId('added-to-position')).toHaveLength(1);
      await waitFor(() => expect(add).toBeEnabled());
      await userEvent.click(add);

      await waitFor(() => expect(screen.getAllByTestId('added-to-position')).toHaveLength(2));
      expect(positionService.addCandidate).toHaveBeenCalledWith('pos-1', 'c-9');
      expect(panel().getAllByTestId('position-candidate-row')).toHaveLength(2);
    });

    it('changes a stage against the loaded version and reloads after a conflict', async () => {
      positionService.listCandidates.mockResolvedValue([link()]);
      positionService.changeStage.mockRejectedValueOnce(
        new AppError('CONFLICT', 'El candidato ha cambiado en esta posición. Vuelva a cargarla.'),
      );
      renderAt('/app/positions/pos-1');

      const select = await screen.findByRole('combobox', { name: 'Estado de Ana García' });
      await userEvent.selectOptions(select, 'interview');

      await waitFor(() =>
        expect(toastService.show).toHaveBeenCalledWith(
          'El candidato ha cambiado en esta posición. Vuelva a cargarla.',
          'error',
        ),
      );
      expect(positionService.changeStage).toHaveBeenCalledWith('pos-1', 'c-1', 'interview', 3);
      await waitFor(() => expect(positionService.listCandidates).toHaveBeenCalledTimes(2));
    });

    it('removes a link only after confirmation', async () => {
      positionService.listCandidates.mockResolvedValue([link()]);
      confirmDialogService.confirm.mockResolvedValueOnce(false);
      renderAt('/app/positions/pos-1');
      const remove = await screen.findByRole('button', {
        name: 'Quitar a Ana García de la posición',
      });

      await userEvent.click(remove);
      expect(positionService.removeCandidate).not.toHaveBeenCalled();

      await userEvent.click(remove);
      await waitFor(() =>
        expect(positionService.removeCandidate).toHaveBeenCalledWith('pos-1', 'c-1'),
      );
      expect(await panel().findByTestId('position-candidates-empty')).toBeVisible();
    });

    it('keeps a closed position read-only and explains how to change it', async () => {
      positionService.get.mockResolvedValue({ ...structuredClone(stored), status: 'closed' });
      positionService.listCandidates.mockResolvedValue([link({ stage: 'hired' })]);
      candidateSearchService.search.mockResolvedValue({
        ...noResults,
        items: [match('c-9', 'Eva')],
        totalCount: 1,
      });
      renderAt('/app/positions/pos-1');

      expect(await screen.findByTestId('position-candidates-closed')).toBeVisible();
      expect(await panel().findByText('Contratado')).toBeVisible();
      expect(panel().queryByRole('combobox')).not.toBeInTheDocument();
      expect(panel().queryByTestId('remove-from-position')).not.toBeInTheDocument();
      expect(panel().queryByTestId('position-candidates-add')).not.toBeInTheDocument();
      await screen.findByText('Eva Pérez');
      expect(screen.queryByTestId('add-to-position')).not.toBeInTheDocument();
    });

    it('offers a reader no link controls', async () => {
      granted = ['positions.read', 'candidates.read'];
      positionService.listCandidates.mockResolvedValue([link()]);
      renderAt('/app/positions/pos-1');

      await screen.findByTestId('position-candidates');
      expect(await panel().findByText('Nuevo')).toBeVisible();
      expect(panel().queryByRole('combobox')).not.toBeInTheDocument();
      expect(panel().queryByTestId('position-candidates-add')).not.toBeInTheDocument();
    });

    it('requests no links and shows no panel without candidates.read', async () => {
      granted = ['positions.read', 'positions.manage'];
      renderAt('/app/positions/pos-1');

      await screen.findByText('No tienes permiso para consultar candidatos.');
      expect(screen.queryByTestId('position-candidates')).not.toBeInTheDocument();
      expect(positionService.listCandidates).not.toHaveBeenCalled();
    });

    it('adds a non-matching candidate through the picker, which marks linked ones', async () => {
      positionService.listCandidates.mockResolvedValue([link()]);
      candidateSearchService.search.mockResolvedValue({
        ...noResults,
        items: [match('c-1', 'Ana'), match('c-7', 'Iris')],
        totalCount: 2,
      });
      positionService.addCandidate.mockResolvedValue(
        link({ candidateId: 'c-7', firstName: 'Iris', lastName: 'Pérez' }),
      );
      renderAt('/app/positions/pos-1');

      await userEvent.click(await screen.findByTestId('position-candidates-add'));
      const dialog = within(await screen.findByRole('dialog'));
      expect(dialog.getByRole('searchbox', { name: 'Buscar por nombre o email' })).toHaveFocus();
      await userEvent.type(dialog.getByRole('searchbox'), 'iris');
      await waitFor(() =>
        expect(candidateSearchService.search).toHaveBeenLastCalledWith(
          expect.objectContaining({ text: 'iris' }),
          expect.objectContaining({ page: 1, pageSize: 10 }),
        ),
      );
      const options = await dialog.findAllByTestId('position-candidate-picker-option');
      expect(options[0]).toHaveAttribute('aria-disabled', 'true');

      await userEvent.click(options[1]);

      await waitFor(() => expect(screen.queryByRole('dialog')).not.toBeInTheDocument());
      expect(positionService.addCandidate).toHaveBeenCalledWith('pos-1', 'c-7');
      expect(await panel().findByRole('link', { name: 'Iris Pérez' })).toBeVisible();
    });

    it('closes the picker with Escape and returns focus to its opener', async () => {
      renderAt('/app/positions/pos-1');
      const opener = await screen.findByTestId('position-candidates-add');

      await userEvent.click(opener);
      await screen.findByRole('dialog');
      await userEvent.keyboard('{Escape}');

      expect(screen.queryByRole('dialog')).not.toBeInTheDocument();
      expect(opener).toHaveFocus();
    });
  });
});
