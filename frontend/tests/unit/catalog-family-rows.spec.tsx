import { render, screen } from '@testing-library/react';
import { CatalogFamilyRows } from '../../src/app/features/catalogs/components/catalog-family-rows';
import { CATALOG_FAMILY_ORDER } from '../../src/app/features/catalogs/components/catalog-family-rows.logic';

describe('CatalogFamilyRows', () => {
  it('renders one row per family in the shared order', () => {
    const { container } = render(<CatalogFamilyRows renderRow={(kind) => <span>{kind}</span>} />);

    const rows = [...container.querySelectorAll('.catalog-family-row')];
    expect(rows.map((row) => row.getAttribute('data-family'))).toEqual([
      'skill',
      'language',
      'program',
      'tag',
    ]);
    expect(rows.map((row) => row.textContent)).toEqual([...CATALOG_FAMILY_ORDER]);
  });

  it('shows the notice once, before the rows', () => {
    const { container } = render(
      <CatalogFamilyRows notice={<p data-testid="notice">aviso</p>} renderRow={() => null} />,
    );

    expect(screen.getAllByTestId('notice')).toHaveLength(1);
    expect(container.querySelector('.catalog-family-rows')?.firstElementChild).toBe(
      screen.getByTestId('notice'),
    );
  });
});
