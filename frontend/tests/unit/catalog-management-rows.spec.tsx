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

/**
 * A catalog value has no page of its own. Since KTL-41 its row is plain: the pencil in «Acciones»
 * opens the inline editor, and the add form opens from «Nuevo».
 */
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

  it('lists catalog families alphabetically with each level family beside its catalog', async () => {
    await renderPage();

    expect(
      within(screen.getByRole('combobox'))
        .getAllByRole('option')
        .map((option) => option.textContent),
    ).toEqual([
      'Estados de formación',
      'Etiquetas',
      'Habilidades',
      'Habilidades (niveles)',
      'Idiomas',
      'Idiomas (niveles)',
      'Programas',
      'Programas (niveles)',
      'Sectores',
      'Tipos de formación',
    ]);
  });

  it('does not start the edit, or navigate, when the row is clicked', async () => {
    await renderPage();
    const row = rows()[0];

    await userEvent.click(within(row).getAllByRole('cell')[0]);
    await userEvent.click(within(row).getAllByRole('cell')[2]);

    expect(within(row).queryByRole('textbox')).toBeNull();
    expect(within(row).queryByRole('button', { name: /^Editar / })).not.toBeNull();
    expect(path).toBe('/app/admin/catalogs');
  });

  it('starts the inline edit from the labelled pencil button', async () => {
    await renderPage();
    const row = rows()[0];
    const name = nameOf(row);

    const edit = within(row).getByRole('button', { name: `Editar ${name}` });
    expect(edit).toHaveAttribute('title', 'Editar');
    expect(edit.textContent).toBe('');
    await userEvent.click(edit);

    expect(within(row).getByRole('textbox', { name: `Nombre de ${name}` })).toHaveValue(name);
    expect(within(row).getByTestId('catalog-edit-save')).toBeInTheDocument();
  });

  it('opens the edit from the keyboard', async () => {
    await renderPage();
    const row = rows()[0];
    const name = nameOf(row);

    within(row)
      .getByRole('button', { name: `Editar ${name}` })
      .focus();
    await userEvent.keyboard('{Enter}');

    expect(within(row).getByTestId('catalog-edit-save')).toBeInTheDocument();
  });

  it('disables the other rows’ actions while one row is being edited', async () => {
    await renderPage();
    await userEvent.click(within(rows()[0]).getByTestId('catalog-edit'));

    for (const button of within(rows()[1]).getAllByRole('button')) {
      expect(button).toBeDisabled();
    }
    expect(screen.getByTestId('catalog-new')).toBeDisabled();
  });

  it('deactivates and reactivates with labelled icon buttons', async () => {
    const catalogService = await renderPage();
    const toggle = vi.spyOn(catalogService, 'toggleActive');
    // The confirm dialog is mounted by the shell, not this page; answer it directly.
    const confirm = vi.spyOn(services.confirmDialogService, 'confirm').mockResolvedValue(true);
    const row = rows()[0];
    const name = nameOf(row);

    const deactivate = within(row).getByRole('button', { name: `Desactivar ${name}` });
    expect(deactivate).toHaveAttribute('title', 'Desactivar');
    expect(deactivate.textContent).toBe('');
    await userEvent.click(deactivate);

    expect(confirm).toHaveBeenCalledTimes(1);
    await waitFor(() => expect(toggle).toHaveBeenCalledTimes(1));
    const activate = await within(rows()[0]).findByRole('button', { name: `Activar ${name}` });
    expect(activate).toHaveAttribute('title', 'Activar');
    await userEvent.click(activate);
    await waitFor(() => expect(toggle).toHaveBeenCalledTimes(2));
    // Reactivation needs no confirmation.
    expect(confirm).toHaveBeenCalledTimes(1);
    confirm.mockRestore();
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
    expect(screen.queryByRole('textbox', { name: /^Nombre de/ })).toBeNull();
  });

  describe('«Nuevo»', () => {
    it('hides the add form until «Nuevo» is pressed', async () => {
      await renderPage();

      expect(screen.queryByTestId('catalog-create-form')).toBeNull();
      const add = screen.getByRole('button', { name: 'Nuevo' });
      expect(add).toHaveAttribute('aria-expanded', 'false');

      await userEvent.click(add);

      expect(screen.getByTestId('catalog-create-form')).toBeInTheDocument();
      expect(screen.getByLabelText('Nombre (es)')).toHaveFocus();
      expect(add).toHaveAttribute('aria-expanded', 'true');
      expect(add).toBeDisabled();
    });

    it('closes the form, empty, on «Cancelar»', async () => {
      await renderPage();
      await userEvent.click(screen.getByRole('button', { name: 'Nuevo' }));
      await userEvent.type(screen.getByLabelText('Nombre (es)'), 'Borrador');

      await userEvent.click(screen.getByTestId('catalog-create-cancel'));

      expect(screen.queryByTestId('catalog-create-form')).toBeNull();
      await userEvent.click(screen.getByRole('button', { name: 'Nuevo' }));
      expect(screen.getByLabelText('Nombre (es)')).toHaveValue('');
    });

    it('closes the form after a value is added', async () => {
      const catalogService = await renderPage();
      await userEvent.click(screen.getByRole('button', { name: 'Nuevo' }));
      await userEvent.type(screen.getByLabelText('Nombre (es)'), 'Neerlandés');

      await userEvent.click(screen.getByRole('button', { name: 'Añadir' }));

      await waitFor(() => expect(screen.queryByTestId('catalog-create-form')).toBeNull());
      expect(catalogService.activeNames('language')).toContain('Neerlandés');
    });

    it('locks the row editors while the add form is open', async () => {
      await renderPage();
      await userEvent.click(screen.getByRole('button', { name: 'Nuevo' }));

      expect(within(rows()[0]).getByTestId('catalog-edit')).toBeDisabled();
    });
  });
});
