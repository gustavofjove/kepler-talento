import { render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { services, type Services } from '../../src/app/core/di/services';
import { ServicesProvider } from '../../src/app/core/di/services-context';
import { SearchCriteriaForm } from '../../src/app/features/search/components/search-criteria-form';
import {
  ALL_CANDIDATE_STATUSES,
  EMPTY_SEARCH_FILTERS,
  type SearchFilters,
} from '../../src/app/features/search/models/search.models';
import { AppError } from '../../src/app/shared/models/error.models';
import type { ReactNode } from 'react';
import {
  createCatalogTestBed,
  FakeCatalogApi,
  loadedCatalogService,
} from './support/catalog-doubles';

/**
 * The shared criteria editor on its own. Its hosts - the search page and the preset editor -
 * are covered by their own specs; what is proven here is what both of them get.
 */
describe('SearchCriteriaForm', () => {
  const filters = () => structuredClone(EMPTY_SEARCH_FILTERS);
  let wrapper: ({ children }: { children: ReactNode }) => ReactNode;

  beforeEach(async () => {
    const catalogService = await loadedCatalogService();
    wrapper = ({ children }) => (
      <ServicesProvider value={{ ...services, catalogService } as unknown as Services}>
        {children}
      </ServicesProvider>
    );
  });

  it('renders the host actions and leading content inside its form', () => {
    const { container } = render(
      <SearchCriteriaForm
        filters={filters()}
        onFiltersChange={vi.fn()}
        onSubmit={vi.fn()}
        leading={<input name="presetName" aria-label="nombre" />}
        actions={<button type="submit">Guardar</button>}
      />,
    );

    const form = container.querySelector('form') as HTMLFormElement;
    expect(form).toContainElement(screen.getByRole('button', { name: 'Guardar' }));
    expect(form).toContainElement(container.querySelector('[name="presetName"]') as HTMLElement);
  });

  it('keeps the field names, status markers and test ids the e2e suite binds to', () => {
    const { container } = render(
      <SearchCriteriaForm
        filters={filters()}
        onFiltersChange={vi.fn()}
        onSubmit={vi.fn()}
        actions={null}
        collapsed={false}
        onCollapsedChange={vi.fn()}
      />,
    );

    expect(container.querySelector('[name="text"]')).not.toBeNull();
    expect(container.querySelector('[name="hasCv"]')).not.toBeNull();
    expect(
      Array.from(container.querySelectorAll('[data-status]')).map((input) =>
        input.getAttribute('data-status'),
      ),
    ).toEqual(ALL_CANDIDATE_STATUSES);
    expect(screen.getByTestId('toggle-filters')).toBeInTheDocument();
    for (const kind of ['skill', 'language', 'program', 'tag']) {
      expect(screen.getByTestId(`search-${kind}-picker`)).toBeInTheDocument();
      expect(screen.getByTestId(`search-${kind}-add`)).toBeInTheDocument();
      // No criterion yet: nothing to combine, so no ANY/ALL control either.
      expect(screen.queryByTestId(`search-${kind}-mode`)).toBeNull();
    }
  });

  it('adds a criterion with any level, sets its level on the chip and never re-adds it', async () => {
    const onFiltersChange = vi.fn();
    const onSubmit = vi.fn();
    const { rerender } = render(
      <SearchCriteriaForm
        filters={filters()}
        onFiltersChange={onFiltersChange}
        onSubmit={onSubmit}
        actions={null}
      />,
      { wrapper },
    );

    await userEvent.click(screen.getByTestId('search-language-add'));
    expect(screen.getByTestId('search-language-input')).toHaveAttribute('name', 'search-language');
    await userEvent.type(screen.getByTestId('search-language-input'), 'ingl');
    await userEvent.keyboard('{ArrowDown}{Enter}');

    const added = onFiltersChange.mock.lastCall![0] as SearchFilters;
    expect(added.languageCriteria).toEqual([{ value: 'Inglés', level: '' }]);
    rerender(
      <SearchCriteriaForm
        filters={added}
        onFiltersChange={onFiltersChange}
        onSubmit={onSubmit}
        actions={null}
      />,
    );
    const chip = screen.getByTestId('search-language-chip');
    expect(chip).toHaveTextContent('Cualquier nivel');

    await userEvent.click(chip);
    const editor = await screen.findByTestId('search-language-editor');
    await userEvent.click(within(editor).getByRole('radio', { name: 'B2' }));

    expect((onFiltersChange.mock.lastCall![0] as SearchFilters).languageCriteria).toEqual([
      { value: 'Inglés', level: 'B2' },
    ]);
    // The value is not offered again, so it cannot become a second criterion.
    if (!screen.queryByTestId('search-language-input'))
      await userEvent.click(screen.getByTestId('search-language-add'));
    await userEvent.type(screen.getByTestId('search-language-input'), 'ingl');
    expect(screen.queryByRole('option', { name: 'Inglés' })).toBeNull();
    expect(onSubmit).not.toHaveBeenCalled();
  });

  it('offers no level for a tag', async () => {
    render(
      <SearchCriteriaForm
        filters={{ ...filters(), tagCriteria: [{ value: 'VIP', level: '' }] }}
        onFiltersChange={vi.fn()}
        onSubmit={vi.fn()}
        actions={null}
      />,
      { wrapper },
    );

    const chip = screen.getByTestId('search-tag-chip');
    expect(chip).toHaveTextContent('VIP');
    expect(chip).not.toHaveTextContent('Cualquier nivel');
    await userEvent.click(chip);
    expect(screen.queryByTestId('search-tag-editor')).toBeNull();
  });

  it('shows the mode control only from two criteria', async () => {
    const onFiltersChange = vi.fn();
    const one = { ...filters(), skillCriteria: [{ value: 'Compras', level: '' }] };
    const { rerender } = render(
      <SearchCriteriaForm
        filters={one}
        onFiltersChange={onFiltersChange}
        onSubmit={vi.fn()}
        actions={null}
      />,
      { wrapper },
    );

    const mode = () =>
      within(screen.getByTestId('search-skill-mode')).getByRole('radio', { name: 'Todos' });
    expect(screen.queryByTestId('search-skill-mode')).toBeNull();

    rerender(
      <SearchCriteriaForm
        filters={{
          ...one,
          skillCriteria: [...one.skillCriteria, { value: 'Análisis', level: 'Alto' }],
        }}
        onFiltersChange={onFiltersChange}
        onSubmit={vi.fn()}
        actions={null}
      />,
    );
    expect(within(screen.getByTestId('search-skill-mode')).getAllByRole('radio')).toHaveLength(2);
    await userEvent.click(mode());
    expect(onFiltersChange).toHaveBeenLastCalledWith(expect.objectContaining({ skillMode: 'ALL' }));
  });

  it('shows the summary only while collapsed', () => {
    const props = {
      filters: { ...filters(), text: 'Marta' },
      onFiltersChange: vi.fn(),
      onSubmit: vi.fn(),
      actions: null,
      onCollapsedChange: vi.fn(),
    };
    const { rerender } = render(<SearchCriteriaForm {...props} collapsed={false} />);
    expect(screen.queryByTestId('filters-summary')).toBeNull();

    rerender(<SearchCriteriaForm {...props} collapsed />);
    expect(screen.getByTestId('filters-summary')).toBeInTheDocument();
  });

  it('disables every picker while the catalogs cannot be loaded', async () => {
    const api = new FakeCatalogApi();
    api.failure = new AppError('INTERNAL_ERROR', 'No se ha podido conectar con el servidor.');
    const { service } = createCatalogTestBed(api);
    render(
      <ServicesProvider value={{ ...services, catalogService: service } as unknown as Services}>
        <SearchCriteriaForm
          filters={{ ...filters(), skillCriteria: [{ value: 'Compras', level: '' }] }}
          onFiltersChange={vi.fn()}
          onSubmit={vi.fn()}
          actions={null}
        />
      </ServicesProvider>,
    );

    await waitFor(() => expect(screen.getByTestId('catalog-status')).toBeInTheDocument());
    for (const kind of ['skill', 'language', 'program', 'tag']) {
      expect(screen.getByTestId(`search-${kind}-add`)).toBeDisabled();
    }
    expect(screen.getByTestId('search-skill-chip')).toHaveTextContent('Compras');
  });

  it('offers no collapse toggle when the host does not control collapsing', () => {
    const { container } = render(
      <SearchCriteriaForm
        filters={filters()}
        onFiltersChange={vi.fn()}
        onSubmit={vi.fn()}
        actions={null}
      />,
    );

    expect(screen.queryByTestId('toggle-filters')).toBeNull();
    expect(container.querySelector('[name="text"]')).not.toBeNull();
  });

  it('hides the editing body and shows the summary while collapsed', () => {
    const { container } = render(
      <SearchCriteriaForm
        filters={filters()}
        onFiltersChange={vi.fn()}
        onSubmit={vi.fn()}
        actions={null}
        collapsed
        onCollapsedChange={vi.fn()}
      />,
    );

    expect(container.querySelector('[name="text"]')).toBeNull();
    expect(screen.getByTestId('filters-summary')).toBeInTheDocument();
    expect(screen.getByTestId('toggle-filters')).toHaveAttribute('aria-expanded', 'false');
  });

  it('reports edits without submitting them', async () => {
    const onFiltersChange = vi.fn();
    const onSubmit = vi.fn();
    render(
      <SearchCriteriaForm
        filters={filters()}
        onFiltersChange={onFiltersChange}
        onSubmit={onSubmit}
        actions={<button type="submit">Buscar</button>}
      />,
    );

    await userEvent.type(screen.getByRole('textbox', { name: 'Texto' }), 'a');
    await userEvent.click(screen.getByRole('checkbox', { name: 'Contratado' }));

    expect(onFiltersChange).toHaveBeenCalledWith(expect.objectContaining({ text: 'a' }));
    expect(onFiltersChange).toHaveBeenLastCalledWith(
      expect.objectContaining({
        statusValues: ALL_CANDIDATE_STATUSES.filter((status) => status !== 'hired'),
      }),
    );
    // Editing a filter never runs a search or saves a preset: only the host's submit does.
    expect(onSubmit).not.toHaveBeenCalled();
  });

  it('hands the current filters to the host on submit', async () => {
    const onSubmit = vi.fn();
    const current = { ...filters(), text: 'Marta' };
    render(
      <SearchCriteriaForm
        filters={current}
        onFiltersChange={vi.fn()}
        onSubmit={onSubmit}
        actions={<button type="submit">Buscar</button>}
      />,
    );

    await userEvent.click(screen.getByRole('button', { name: 'Buscar' }));

    expect(onSubmit).toHaveBeenCalledWith(current);
  });
});
