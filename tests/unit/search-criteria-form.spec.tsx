import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { SearchCriteriaForm } from '../../src/app/features/search/components/search-criteria-form';
import {
  ALL_CANDIDATE_STATUSES,
  EMPTY_SEARCH_FILTERS,
} from '../../src/app/features/search/models/search.models';

/**
 * The shared criteria editor on its own. Its hosts - the search page and the preset editor -
 * are covered by their own specs; what is proven here is what both of them get.
 */
describe('SearchCriteriaForm', () => {
  const filters = () => structuredClone(EMPTY_SEARCH_FILTERS);

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
    expect(screen.getByTestId('filters-summary')).toBeInTheDocument();
    expect(screen.getByTestId('toggle-filters')).toBeInTheDocument();
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

  it('hides the editing body but keeps the summary while collapsed', () => {
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
