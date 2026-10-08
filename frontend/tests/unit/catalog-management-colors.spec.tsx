import { render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter } from 'react-router';
import { services, type Services } from '../../src/app/core/di/services';
import { ServicesProvider } from '../../src/app/core/di/services-context';
import { CatalogManagementPage } from '../../src/app/features/catalogs/pages/catalog-management-page';
import type { CatalogFamily } from '../../src/app/features/catalogs/models/catalog.models';
import { createCatalogTestBed } from './support/catalog-doubles';

/** KTL-41: chip families carry a colour, chosen in the row editor and the add form. */
describe('CatalogManagementPage colours', () => {
  const renderPage = async (family: CatalogFamily = 'skill', { create = false } = {}) => {
    const bed = createCatalogTestBed();
    await bed.service.ensureLoaded();
    render(
      <ServicesProvider value={{ ...services, catalogService: bed.service } as unknown as Services}>
        <MemoryRouter initialEntries={['/app/admin/catalogs']}>
          <CatalogManagementPage />
        </MemoryRouter>
      </ServicesProvider>,
    );
    await userEvent.selectOptions(screen.getByRole('combobox'), family);
    // The add form is hidden until «Nuevo» opens it.
    if (create) await userEvent.click(screen.getByTestId('catalog-new'));
    return bed;
  };

  const rows = () => screen.getAllByTestId('catalog-row');
  const chooseInDialog = async (color: string) => {
    const dialog = await screen.findByTestId('catalog-color-dialog');
    await userEvent.click(
      within(dialog)
        .getAllByTestId('catalog-color-option')
        .find((option) => option.getAttribute('data-value') === color)!,
    );
    await waitFor(() => expect(screen.queryByTestId('catalog-color-dialog')).toBeNull());
  };

  it('shows a «Color» column with a named circle per value for a chip family', async () => {
    await renderPage('skill', { create: true });

    expect(screen.getByRole('columnheader', { name: 'Color' })).toBeInTheDocument();
    const swatch = within(rows()[0]).getByTestId('catalog-color');
    expect(swatch).toHaveAttribute('title', 'Naranja');
    expect(swatch).toHaveAccessibleName('Color: Naranja');
    expect(screen.getByTestId('new-catalog-color-trigger')).toHaveAccessibleName(
      'Elegir el color del nuevo valor (actual: Naranja)',
    );
  });

  it.each<CatalogFamily>([
    'language_level',
    'program_level',
    'skill_level',
    'education_type',
    'education_status',
    'sector',
  ])('offers no colour for the %s family', async (family) => {
    await renderPage(family, { create: true });

    expect(screen.queryByRole('columnheader', { name: 'Color' })).toBeNull();
    expect(screen.queryByTestId('catalog-color')).toBeNull();
    expect(screen.queryByTestId('new-catalog-color-trigger')).toBeNull();
  });

  it('saves a colour chosen while editing a row, with the row version', async () => {
    const { service, api } = await renderPage('skill');
    const update = vi.spyOn(api, 'update');
    const row = rows()[0];
    const name = service.list('skill', true)[0].nameEs;

    await userEvent.click(within(row).getByRole('button', { name: `Editar ${name}` }));
    const trigger = within(row).getByTestId('catalog-color-trigger');
    expect(trigger).toHaveAccessibleName(`Cambiar el color de ${name} (actual: Naranja)`);
    await userEvent.click(trigger);
    await chooseInDialog('blue');
    // Chosen, not yet saved.
    expect(update).not.toHaveBeenCalled();
    expect(within(row).getByTestId('catalog-color-trigger')).toHaveAttribute('title', 'Azul');

    await userEvent.click(within(row).getByTestId('catalog-edit-save'));

    await waitFor(() =>
      expect(update).toHaveBeenCalledWith(
        'skill',
        expect.any(String),
        expect.objectContaining({ color: 'blue', version: 1 }),
      ),
    );
    await waitFor(() =>
      expect(within(rows()[0]).getByTestId('catalog-color')).toHaveAttribute('title', 'Azul'),
    );
  });

  it('sends no colour when the row is saved with its colour unchanged', async () => {
    const { api } = await renderPage('skill');
    const update = vi.spyOn(api, 'update');

    await userEvent.click(within(rows()[0]).getByTestId('catalog-edit'));
    await userEvent.click(within(rows()[0]).getByTestId('catalog-edit-save'));

    await waitFor(() => expect(update).toHaveBeenCalled());
    expect(update.mock.calls[0][2].color).toBeUndefined();
  });

  it('discards a chosen colour when the row edit is cancelled', async () => {
    const { api } = await renderPage('program');
    const update = vi.spyOn(api, 'update');

    await userEvent.click(within(rows()[0]).getByTestId('catalog-edit'));
    await userEvent.click(within(rows()[0]).getByTestId('catalog-color-trigger'));
    await chooseInDialog('violet');
    await userEvent.click(within(rows()[0]).getByTestId('catalog-edit-cancel'));

    expect(within(rows()[0]).getByTestId('catalog-color')).toHaveAttribute('title', 'Naranja');
    expect(update).not.toHaveBeenCalled();
  });

  it('creates a value in the chosen colour, and the next form starts at Naranja', async () => {
    const { service, api } = await renderPage('tag', { create: true });
    const create = vi.spyOn(api, 'create');

    await userEvent.type(screen.getByLabelText('Nombre (es)'), 'Urgente');
    await userEvent.click(screen.getByTestId('new-catalog-color-trigger'));
    await chooseInDialog('pink');
    await userEvent.click(screen.getByRole('button', { name: 'Añadir' }));

    await waitFor(() =>
      expect(create).toHaveBeenCalledWith(
        'tag',
        expect.objectContaining({ nameEs: 'Urgente', color: 'pink' }),
      ),
    );
    expect(service.colorOf('tag', 'Urgente')).toBe('pink');
    await waitFor(() => expect(screen.queryByTestId('catalog-create-form')).toBeNull());
    await userEvent.click(screen.getByTestId('catalog-new'));
    expect(screen.getByTestId('new-catalog-color-trigger')).toHaveAttribute('title', 'Naranja');
  });

  it('resets the add form colour when the family changes', async () => {
    await renderPage('tag', { create: true });
    await userEvent.click(screen.getByTestId('new-catalog-color-trigger'));
    await chooseInDialog('green');

    await userEvent.selectOptions(screen.getByRole('combobox'), 'language');

    expect(screen.getByTestId('new-catalog-color-trigger')).toHaveAttribute('title', 'Naranja');
  });

  it('sends no colour when adding to a family without colours', async () => {
    const { api } = await renderPage('sector', { create: true });
    const create = vi.spyOn(api, 'create');

    await userEvent.type(screen.getByLabelText('Nombre (es)'), 'Logística');
    await userEvent.click(screen.getByRole('button', { name: 'Añadir' }));

    await waitFor(() => expect(create).toHaveBeenCalled());
    expect(create.mock.calls[0][1].color).toBeUndefined();
  });
});
