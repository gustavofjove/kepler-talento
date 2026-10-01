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
          availabilityValues: ['available', 'unavailable'],
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
    expect(group('Disponibilidad')).toHaveTextContent('Disponible');
    expect(group('Disponibilidad')).toHaveTextContent('No disponible');
    expect(group('CV')).toHaveTextContent('Con CV');
    // The combination mode is only worth stating when there is more than one criterion.
    expect(group('Habilidades (Todos)')).toHaveTextContent('Java · ≥ Avanzado');
    expect(group('Habilidades (Todos)')).toHaveTextContent('SQL');
    expect(group('Idiomas')).toHaveTextContent('Inglés · ≥ B2');
    expect(group('Etiquetas')).toHaveTextContent('Recontratable');
  });

  it('omits the availability group when every value is selected, since that restricts nothing', () => {
    render(<SearchCriteriaSummary filters={filters({ text: 'x' })} />);

    expect(screen.queryByRole('heading', { name: 'Disponibilidad' })).toBeNull();
  });

  it('treats an empty availability array as unrestricted', () => {
    render(<SearchCriteriaSummary filters={filters({ availabilityValues: [] })} />);
    expect(screen.getByTestId('filters-summary')).toHaveTextContent('Sin filtros aplicados.');
    expect(screen.queryByRole('heading', { name: 'Disponibilidad' })).toBeNull();
  });
});
