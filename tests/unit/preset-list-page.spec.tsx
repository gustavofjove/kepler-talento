import { render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter } from 'react-router';
import { services, type Services } from '../../src/app/core/di/services';
import { ServicesProvider } from '../../src/app/core/di/services-context';
import { signal } from '../../src/app/core/state/signal';
import { PresetListPage } from '../../src/app/features/admin/presets/preset-list-page';
import {
  EMPTY_SEARCH_FILTERS,
  type SearchPreset,
} from '../../src/app/features/search/models/search.models';
import type { PresetsState } from '../../src/app/features/search/services/search-presets.service';

describe('PresetListPage', () => {
  const preset = (overrides: Partial<SearchPreset> = {}): SearchPreset => ({
    id: 'p-1',
    name: 'Java senior',
    filters: { ...structuredClone(EMPTY_SEARCH_FILTERS), hasCv: 'yes' },
    createdAt: '2026-03-01T09:00:00Z',
    updatedAt: '2026-03-02T10:30:00Z',
    version: 3,
    ...overrides,
  });

  const toastService = { show: vi.fn() };
  const confirmDialogService = { confirm: vi.fn() };
  let searchPresetsService: {
    state: ReturnType<typeof signal<PresetsState>>;
    load: ReturnType<typeof vi.fn>;
    removePreset: ReturnType<typeof vi.fn>;
  };

  const renderPage = (state: PresetsState) => {
    searchPresetsService = {
      state: signal<PresetsState>(state),
      load: vi.fn().mockResolvedValue(state.presets),
      removePreset: vi.fn().mockResolvedValue(undefined),
    };
    return render(
      <ServicesProvider
        value={
          {
            ...services,
            searchPresetsService,
            toastService,
            confirmDialogService,
          } as unknown as Services
        }
      >
        <MemoryRouter>
          <PresetListPage />
        </MemoryRouter>
      </ServicesProvider>,
    );
  };

  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('loads the library and lists each preset compactly, without its criteria', async () => {
    renderPage({
      status: 'loaded',
      presets: [
        preset(),
        preset({ id: 'p-2', name: 'Inglés B2', lastUsedAt: '2026-03-04T08:00:00Z' }),
      ],
    });

    expect(searchPresetsService.load).toHaveBeenCalled();
    const rows = screen.getAllByTestId('preset-row');
    expect(rows).toHaveLength(2);
    // Alphabetical by default, without regard to accents.
    expect(within(rows[0]).getByTestId('preset-row-name')).toHaveTextContent('Inglés B2');
    const java = rows[1];
    expect(within(java).getByTestId('preset-edit')).toHaveAttribute(
      'href',
      '/app/admin/presets/p-1/edit',
    );
    expect(java).toHaveTextContent('Nunca');
    // Criteria are one click away in the dialog, not rendered in every row.
    expect(screen.queryByTestId('filters-summary')).toBeNull();
  });

  it('opens the criteria of a preset from the eye button beside its name', async () => {
    renderPage({ status: 'loaded', presets: [preset()] });
    const view = screen.getByRole('button', { name: 'Ver criterios de Java senior' });

    await userEvent.click(view);

    const dialog = screen.getByRole('dialog', { name: 'Java senior' });
    expect(within(dialog).getByTestId('filters-summary')).toHaveTextContent('Con CV');
    expect(within(dialog).getByTestId('preset-view-last-used')).toHaveTextContent('Nunca');
    expect(within(dialog).getByTestId('preset-view-edit')).toHaveAttribute(
      'href',
      '/app/admin/presets/p-1/edit',
    );

    await userEvent.keyboard('{Escape}');

    expect(screen.queryByRole('dialog')).toBeNull();
    expect(view).toHaveFocus();
  });

  it('shows an empty state when the library has no presets', () => {
    renderPage({ status: 'loaded', presets: [] });

    expect(screen.getByTestId('presets-list-empty')).toHaveTextContent(
      'Todavía no hay presets. Crea el primero.',
    );
  });

  it('shows an error instead of an empty list when loading failed', () => {
    renderPage({ status: 'failed', presets: [] });

    expect(screen.getByTestId('presets-list-error')).toBeInTheDocument();
    expect(screen.queryByTestId('presets-list-empty')).toBeNull();
  });

  it('filters rows by name ignoring accents', async () => {
    renderPage({
      status: 'loaded',
      presets: [preset(), preset({ id: 'p-2', name: 'Inglés B2' })],
    });

    await userEvent.type(screen.getByRole('textbox', { name: 'Filtrar por nombre' }), 'ingles');

    expect(screen.getAllByTestId('preset-row')).toHaveLength(1);
    expect(screen.getByTestId('preset-row-name')).toHaveTextContent('Inglés B2');
  });

  it('sends nothing when the deletion is not confirmed', async () => {
    confirmDialogService.confirm.mockResolvedValue(false);
    renderPage({ status: 'loaded', presets: [preset()] });

    await userEvent.click(screen.getByTestId('preset-delete'));

    await waitFor(() => expect(confirmDialogService.confirm).toHaveBeenCalled());
    expect(searchPresetsService.removePreset).not.toHaveBeenCalled();
  });

  it('deletes a confirmed preset against the version it listed', async () => {
    confirmDialogService.confirm.mockResolvedValue(true);
    renderPage({ status: 'loaded', presets: [preset()] });

    await userEvent.click(screen.getByTestId('preset-delete'));

    await waitFor(() => expect(searchPresetsService.removePreset).toHaveBeenCalledWith('p-1', 3));
    expect(confirmDialogService.confirm).toHaveBeenCalledWith(
      expect.objectContaining({ danger: true }),
    );
    expect(toastService.show).toHaveBeenCalledWith('Preset eliminado: Java senior.', 'success');
  });
});
