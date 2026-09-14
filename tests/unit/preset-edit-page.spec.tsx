import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter, Route, Routes, useLocation } from 'react-router';
import { services, type Services } from '../../src/app/core/di/services';
import { ServicesProvider } from '../../src/app/core/di/services-context';
import { signal } from '../../src/app/core/state/signal';
import { AppError } from '../../src/app/shared/models/error.models';
import { PresetEditPage } from '../../src/app/features/admin/presets/preset-edit-page';
import { AdvancedSearchPage } from '../../src/app/features/search/pages/advanced-search-page';
import {
  EMPTY_SEARCH_FILTERS,
  type SearchPreset,
} from '../../src/app/features/search/models/search.models';

function LocationProbe() {
  return <p data-testid="location">{useLocation().pathname}</p>;
}

describe('PresetEditPage', () => {
  const stored: SearchPreset = {
    id: 'p-1',
    name: 'Java senior',
    filters: { ...structuredClone(EMPTY_SEARCH_FILTERS), text: 'java' },
    createdAt: '2026-03-01T09:00:00Z',
    updatedAt: '2026-03-01T09:00:00Z',
    version: 4,
  };

  const toastService = { show: vi.fn() };
  const authService = {
    profile: signal<unknown>({ id: 'u-1', isActive: true }),
    hasPermission: vi.fn().mockReturnValue(true),
  };
  let searchPresetsService: {
    state: unknown;
    load: ReturnType<typeof vi.fn>;
    loadLastFilters: ReturnType<typeof vi.fn>;
    rememberLastFilters: ReturnType<typeof vi.fn>;
    emptyFilters: ReturnType<typeof vi.fn>;
    get: ReturnType<typeof vi.fn>;
    createPreset: ReturnType<typeof vi.fn>;
    updatePreset: ReturnType<typeof vi.fn>;
  };

  beforeEach(() => {
    vi.clearAllMocks();
    searchPresetsService = {
      state: signal({ status: 'loaded', presets: [] }),
      load: vi.fn().mockResolvedValue([]),
      loadLastFilters: vi.fn(() => structuredClone(EMPTY_SEARCH_FILTERS)),
      rememberLastFilters: vi.fn(),
      emptyFilters: vi.fn(() => structuredClone(EMPTY_SEARCH_FILTERS)),
      get: vi.fn().mockResolvedValue(stored),
      createPreset: vi.fn().mockResolvedValue({ ...stored, id: 'p-new', name: 'Nuevo' }),
      updatePreset: vi.fn().mockResolvedValue(stored),
    };
  });

  const renderAt = (path: string) =>
    render(
      <ServicesProvider
        value={
          {
            ...services,
            searchPresetsService,
            toastService,
            authService,
            candidateSearchService: {
              search: vi
                .fn()
                .mockResolvedValue({ items: [], page: 1, pageSize: 25, totalCount: 0 }),
              emptyFilters: () => structuredClone(EMPTY_SEARCH_FILTERS),
            },
          } as unknown as Services
        }
      >
        <MemoryRouter initialEntries={[path]}>
          <LocationProbe />
          <Routes>
            <Route path="/app/admin/presets/new" element={<PresetEditPage />} />
            <Route path="/app/admin/presets/:id/edit" element={<PresetEditPage />} />
            <Route path="/app/search" element={<AdvancedSearchPage />} />
            <Route path="*" element={null} />
          </Routes>
        </MemoryRouter>
      </ServicesProvider>,
    );

  const nameInput = () => screen.getByTestId('preset-name');

  it('creates a preset from the name and criteria and returns to the list', async () => {
    renderAt('/app/admin/presets/new');

    await userEvent.type(nameInput(), 'Nuevo');
    await userEvent.type(screen.getByRole('textbox', { name: 'Texto' }), 'ana');
    await userEvent.click(screen.getByTestId('preset-save'));

    await waitFor(() =>
      expect(screen.getByTestId('location')).toHaveTextContent(/^\/app\/admin\/presets$/),
    );
    expect(searchPresetsService.createPreset).toHaveBeenCalledWith(
      'Nuevo',
      expect.objectContaining({ text: 'ana' }),
    );
  });

  it('warns that presets are visible to everyone with search access', () => {
    renderAt('/app/admin/presets/new');

    expect(nameInput()).toHaveAccessibleDescription(/visibles para todas las personas/);
  });

  it('loads the preset and saves it against the version it loaded', async () => {
    renderAt('/app/admin/presets/p-1/edit');

    await waitFor(() => expect(nameInput()).toHaveValue('Java senior'));
    expect(screen.getByRole('textbox', { name: 'Texto' })).toHaveValue('java');
    await userEvent.type(nameInput(), ' 2');
    await userEvent.click(screen.getByTestId('preset-save'));

    await waitFor(() =>
      expect(searchPresetsService.updatePreset).toHaveBeenCalledWith(
        'p-1',
        'Java senior 2',
        expect.objectContaining({ text: 'java' }),
        4,
      ),
    );
  });

  it.each([
    ['search_preset.name.conflict', 'Ya existe un preset con ese nombre.'],
    [
      'search_preset.concurrency.conflict',
      'Otra persona ha modificado este preset. Recarga para ver los cambios.',
    ],
  ])('explains a %s refusal and keeps what was entered', async (code, message) => {
    (searchPresetsService.updatePreset as ReturnType<typeof vi.fn>).mockRejectedValue(
      new AppError('CONFLICT', 'Conflicto.', undefined, undefined, code),
    );
    renderAt('/app/admin/presets/p-1/edit');
    await waitFor(() => expect(nameInput()).toHaveValue('Java senior'));

    await userEvent.type(nameInput(), ' B');
    await userEvent.click(screen.getByTestId('preset-save'));

    expect(await screen.findByTestId('preset-form-error')).toHaveTextContent(message);
    expect(nameInput()).toHaveValue('Java senior B');
    expect(screen.getByTestId('location')).toHaveTextContent('/app/admin/presets/p-1/edit');
  });

  it('returns to the list when the preset no longer exists', async () => {
    (searchPresetsService.get as ReturnType<typeof vi.fn>).mockRejectedValue(
      new AppError('NOT_FOUND', 'No encontrado.'),
    );

    renderAt('/app/admin/presets/p-gone/edit');

    await waitFor(() =>
      expect(screen.getByTestId('location')).toHaveTextContent(/^\/app\/admin\/presets$/),
    );
    expect(toastService.show).toHaveBeenCalledWith('El preset ya no existe.', 'warning');
  });

  it('edits criteria with exactly the controls the search page uses', async () => {
    const criteriaControls = (container: HTMLElement) =>
      Array.from(container.querySelectorAll('form [name], form [data-testid], form [data-status]'))
        .map(
          (element) =>
            element.getAttribute('name') ??
            element.getAttribute('data-testid') ??
            `status:${element.getAttribute('data-status')}`,
        )
        // The host's own fields and buttons are the only permitted difference.
        .filter((id) => !['presetName', 'preset-name', 'preset-save', 'preset-cancel'].includes(id))
        .sort();

    const search = renderAt('/app/search');
    await waitFor(() => expect(searchPresetsService.load).toHaveBeenCalled());
    const fromSearch = criteriaControls(search.container);
    search.unmount();

    const edit = renderAt('/app/admin/presets/new');
    const fromEditor = criteriaControls(edit.container);

    expect(fromSearch.length).toBeGreaterThan(0);
    expect(fromEditor).toEqual(fromSearch);
  });
});
