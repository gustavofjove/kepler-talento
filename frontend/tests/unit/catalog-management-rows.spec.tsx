import { render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter, useLocation } from 'react-router';
import { services, type Services } from '../../src/app/core/di/services';
import { ServicesProvider } from '../../src/app/core/di/services-context';
import { CatalogManagementPage } from '../../src/app/features/catalogs/pages/catalog-management-page';
import { loadedCatalogService } from './support/catalog-doubles';

let path = '';
function PathProbe() {
  path = useLocation().pathname;
  return null;
}

/** KTL-31: a catalog value has no page of its own, so its row opens the inline editor. */
describe('CatalogManagementPage rows', () => {
  const renderPage = async () => {
    const catalogService = await loadedCatalogService();
    render(
      <ServicesProvider value={{ ...services, catalogService } as unknown as Services}>
        <MemoryRouter initialEntries={['/app/admin/catalogs']}>
          <CatalogManagementPage />
          <PathProbe />
        </MemoryRouter>
      </ServicesProvider>,
    );
    return catalogService;
  };

  const rows = () => screen.getAllByTestId('catalog-row');
  const nameOf = (row: HTMLElement) => within(row).getAllByRole('cell')[2].textContent ?? '';

  it('starts the inline edit when a plain part of the row is clicked, without navigating', async () => {
    await renderPage();
    const row = rows()[0];
    const name = nameOf(row);

    await userEvent.click(within(row).getAllByRole('cell')[0]);

    expect(within(row).getByRole('textbox', { name: `Nombre de ${name}` })).toHaveValue(name);
    expect(within(row).getByTestId('catalog-edit-save')).toBeInTheDocument();
    expect(path).toBe('/app/admin/catalogs');
  });

  it('offers no «Editar» button: the name is the keyboard way into the edit', async () => {
    await renderPage();
    const row = rows()[0];
    const name = nameOf(row);

    expect(within(row).queryByRole('button', { name: 'Editar' })).toBeNull();
    within(row)
      .getByRole('button', { name: `Editar ${name}` })
      .focus();
    await userEvent.keyboard('{Enter}');

    expect(within(row).getByTestId('catalog-edit-save')).toBeInTheDocument();
  });

  it('ignores row clicks on other rows while one row is being edited', async () => {
    await renderPage();
    await userEvent.click(within(rows()[0]).getAllByRole('cell')[0]);

    await userEvent.click(within(rows()[1]).getAllByRole('cell')[0]);

    expect(within(rows()[1]).queryByRole('textbox')).toBeNull();
    expect(within(rows()[0]).getByTestId('catalog-edit-save')).toBeInTheDocument();
  });

  it('keeps the other row controls from starting the edit', async () => {
    await renderPage();
    const row = rows()[1];

    await userEvent.click(within(row).getByTestId('catalog-move-up'));

    await waitFor(() => expect(screen.queryByRole('textbox', { name: /^Nombre de/ })).toBeNull());
  });

  it('reorders with labelled arrow buttons instead of «Subir» and «Bajar» text', async () => {
    const catalogService = await renderPage();
    const move = vi.spyOn(catalogService, 'move');
    const row = rows()[1];
    const name = nameOf(row);

    const up = within(row).getByRole('button', { name: `Subir ${name}` });
    const down = within(row).getByRole('button', { name: `Bajar ${name}` });
    expect(up).toHaveAttribute('title', 'Subir');
    expect(up.textContent).toBe('');
    expect(down.textContent).toBe('');

    await userEvent.click(down);

    expect(move).toHaveBeenCalledWith('language', expect.any(String), 1);
  });
});
