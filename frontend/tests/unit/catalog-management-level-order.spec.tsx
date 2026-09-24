import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { services, type Services } from '../../src/app/core/di/services';
import { ServicesProvider } from '../../src/app/core/di/services-context';
import { CatalogManagementPage } from '../../src/app/features/catalogs/pages/catalog-management-page';
import { loadedCatalogService } from './support/catalog-doubles';

describe('CatalogManagementPage level order', () => {
  it('explains search ranking only for level families', async () => {
    const catalogService = await loadedCatalogService();
    render(
      <ServicesProvider value={{ ...services, catalogService } as unknown as Services}>
        <CatalogManagementPage />
      </ServicesProvider>,
    );

    const family = screen.getByRole('combobox');
    const hint = 'El orden define qué niveles se consideran superiores en la búsqueda';
    for (const levelFamily of ['language_level', 'program_level', 'skill_level']) {
      await userEvent.selectOptions(family, levelFamily);
      expect(screen.getByText((text) => text.includes(hint))).toBeInTheDocument();
    }
    await userEvent.selectOptions(family, 'language');
    expect(screen.queryByText((text) => text.includes(hint))).toBeNull();
  });
});
