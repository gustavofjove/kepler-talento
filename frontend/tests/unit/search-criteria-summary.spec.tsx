import { act, render, screen, within } from '@testing-library/react';
import { services, type Services } from '../../src/app/core/di/services';
import { ServicesProvider } from '../../src/app/core/di/services-context';
import { SearchCriteriaSummary } from '../../src/app/features/search/components/search-criteria-summary';
import {
  EMPTY_SEARCH_FILTERS,
  type SearchFilters,
} from '../../src/app/features/search/models/search.models';
import { loadedCatalogService } from './support/catalog-doubles';

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

  describe('catalog colours (KTL-41)', () => {
    const renderWithCatalogs = async (summaryFilters: SearchFilters, layout?: 'inline') => {
      const catalogService = await loadedCatalogService();
      const analysis = catalogService
        .list('skill', true)
        .find((item) => item.nameEs === 'Análisis')!;
      await catalogService.update('skill', analysis.id, { nameEs: 'Análisis', color: 'blue' });
      render(
        <ServicesProvider value={{ ...services, catalogService } as unknown as Services}>
          <SearchCriteriaSummary filters={summaryFilters} layout={layout} />
        </ServicesProvider>,
      );
      return catalogService;
    };
    const chipFor = (text: string) =>
      Array.from(document.querySelectorAll<HTMLElement>('.filters-summary .chip')).find(
        (element) => element.textContent === text,
      )!;

    it.each([undefined, 'inline' as const])(
      'draws catalog criteria in their colour and the rest in the default (%s layout)',
      async (layout) => {
        await renderWithCatalogs(
          filters({
            text: 'Marta',
            hasCv: 'yes',
            skillCriteria: [
              { value: 'Análisis', level: 'Alto' },
              { value: 'Compras', level: '' },
            ],
          }),
          layout,
        );

        expect(chipFor('Análisis · ≥ Alto')).toHaveAttribute('data-catalog-color', 'blue');
        expect(chipFor('Compras')).toHaveAttribute('data-catalog-color', 'orange');
        expect(chipFor('Marta')).not.toHaveAttribute('data-catalog-color');
        expect(chipFor('Con CV')).not.toHaveAttribute('data-catalog-color');
      },
    );

    it('falls back to orange for a criterion whose value no longer exists', async () => {
      await renderWithCatalogs(
        filters({ tagCriteria: [{ value: 'Etiqueta retirada', level: '' }] }),
      );

      expect(chipFor('Etiqueta retirada')).toHaveAttribute('data-catalog-color', 'orange');
    });

    it('follows a recolour without being re-rendered by its host', async () => {
      const catalogService = await renderWithCatalogs(
        filters({ skillCriteria: [{ value: 'Análisis', level: '' }] }),
      );
      const analysis = catalogService
        .list('skill', true)
        .find((item) => item.nameEs === 'Análisis')!;

      await act(() =>
        catalogService.update('skill', analysis.id, { nameEs: 'Análisis', color: 'pink' }),
      );

      expect(chipFor('Análisis')).toHaveAttribute('data-catalog-color', 'pink');
    });
  });
});
