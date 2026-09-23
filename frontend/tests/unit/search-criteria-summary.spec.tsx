import { render, screen, within } from '@testing-library/react';
import { SearchCriteriaSummary } from '../../src/app/features/search/components/search-criteria-summary';
import {
  EMPTY_SEARCH_FILTERS,
  type SearchFilters,
} from '../../src/app/features/search/models/search.models';

const filters = (overrides: Partial<SearchFilters> = {}): SearchFilters => ({
  ...structuredClone(EMPTY_SEARCH_FILTERS),
  ...overrides,
});

describe('SearchCriteriaSummary', () => {
  it('says so when no filter restricts the search', () => {
    render(<SearchCriteriaSummary filters={filters()} />);

    expect(screen.getByTestId('filters-summary')).toHaveTextContent('Sin filtros aplicados.');
  });

  it('renders one group per applied filter family, in Spanish', () => {
    render(
      <SearchCriteriaSummary
        filters={filters({
          text: '  Marta  ',
          statusValues: ['available', 'in_process'],
          hasCv: 'yes',
          skillCriteria: [
            { value: 'Java', level: 'Avanzado' },
            { value: 'SQL', level: '' },
          ],
          skillMode: 'ALL',
          languageCriteria: [{ value: 'Inglés', level: 'B2' }],
          tagCriteria: [{ value: 'Recontratable', level: '' }],
        })}
      />,
    );

    const summary = screen.getByTestId('filters-summary');
    const group = (label: string) =>
      within(summary).getByRole('heading', { name: label }).parentElement as HTMLElement;

    expect(group('Texto')).toHaveTextContent('Marta');
    expect(group('Estados')).toHaveTextContent('Disponible');
    expect(group('Estados')).toHaveTextContent('En proceso');
    expect(group('CV')).toHaveTextContent('Con CV');
    // The combination mode is only worth stating when there is more than one criterion.
    expect(group('Habilidad (Todos)')).toHaveTextContent('Java · Avanzado');
    expect(group('Habilidad (Todos)')).toHaveTextContent('SQL');
    expect(group('Idiomas')).toHaveTextContent('Inglés · B2');
    expect(group('Etiquetas')).toHaveTextContent('Recontratable');
  });

  it('omits the status group when every status is selected, since that restricts nothing', () => {
    render(<SearchCriteriaSummary filters={filters({ text: 'x' })} />);

    expect(screen.queryByRole('heading', { name: 'Estados' })).toBeNull();
  });
});
