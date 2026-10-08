import { render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { useState } from 'react';
import {
  CatalogColorPickerSwatch,
  CatalogColorSwatch,
} from '../../src/app/features/catalogs/components/catalog-color-swatch';
import type { CatalogColor } from '../../src/app/features/catalogs/models/catalog.models';

/** The swatch as a controlled field, the way the catalog page hosts it. */
function Host({ initial = 'orange' as CatalogColor, onChange = (_: CatalogColor) => {} }) {
  const [color, setColor] = useState<CatalogColor>(initial);
  return (
    <CatalogColorPickerSwatch
      color={color}
      label={`Cambiar el color de Inglés (actual: ${color})`}
      testId="catalog-color-trigger"
      onChange={(next) => {
        setColor(next);
        onChange(next);
      }}
    />
  );
}

const trigger = () => screen.getByTestId('catalog-color-trigger');
const openDialog = async () => {
  await userEvent.click(trigger());
  return screen.findByTestId('catalog-color-dialog');
};
const options = () => screen.getAllByTestId('catalog-color-option');

describe('catalog colour swatch and dialog (KTL-41)', () => {
  it('shows a read-only circle named by its tooltip and accessible name', () => {
    render(<CatalogColorSwatch color="blue" />);

    const swatch = screen.getByRole('img', { name: 'Color: Azul' });
    expect(swatch).toHaveAttribute('title', 'Azul');
    expect(swatch).toHaveAttribute('data-catalog-color', 'blue');
  });

  it('offers the nine colours by Spanish name, in palette order, with the current one checked', async () => {
    render(<Host initial="teal" />);

    const dialog = await openDialog();

    expect(within(dialog).getByRole('heading', { name: 'Elegir color' })).toBeInTheDocument();
    expect(
      within(dialog)
        .getAllByRole('option')
        .map((option) => option.textContent),
    ).toEqual([
      'Naranja',
      'Amarillo',
      'Verde',
      'Turquesa',
      'Azul',
      'Índigo',
      'Violeta',
      'Rosa',
      'Gris',
    ]);
    const selected = within(dialog).getByRole('option', { selected: true });
    expect(selected).toHaveAttribute('data-value', 'teal');
    // The selection is marked by an icon too, not by colour alone.
    expect(selected.querySelector('.catalog-color-option-check svg')).not.toBeNull();
  });

  it('chooses a colour on click, closes and returns focus to the circle', async () => {
    const onChange = vi.fn();
    render(<Host onChange={onChange} />);
    await openDialog();

    await userEvent.click(
      options().find((option) => option.getAttribute('data-value') === 'blue')!,
    );

    expect(onChange).toHaveBeenCalledWith('blue');
    await waitFor(() => expect(screen.queryByTestId('catalog-color-dialog')).toBeNull());
    expect(trigger()).toHaveAttribute('data-catalog-color', 'blue');
    expect(trigger()).toHaveAttribute('title', 'Azul');
    await waitFor(() => expect(trigger()).toHaveFocus());
  });

  it('moves between colours with the arrow keys without choosing, and chooses with Enter', async () => {
    const onChange = vi.fn();
    render(<Host onChange={onChange} />);
    await openDialog();
    await waitFor(() => expect(options()[0]).toHaveFocus());

    await userEvent.keyboard('{ArrowRight}');

    expect(options()[1]).toHaveFocus();
    expect(onChange).not.toHaveBeenCalled();
    expect(screen.getByTestId('catalog-color-dialog')).toBeInTheDocument();

    await userEvent.keyboard('{Enter}');

    expect(onChange).toHaveBeenCalledWith('yellow');
    await waitFor(() => expect(screen.queryByTestId('catalog-color-dialog')).toBeNull());
  });

  it('closes on Escape without changing the colour', async () => {
    const onChange = vi.fn();
    render(<Host initial="pink" onChange={onChange} />);
    await openDialog();

    await userEvent.keyboard('{Escape}');

    await waitFor(() => expect(screen.queryByTestId('catalog-color-dialog')).toBeNull());
    expect(onChange).not.toHaveBeenCalled();
    expect(trigger()).toHaveAttribute('data-catalog-color', 'pink');
    await waitFor(() => expect(trigger()).toHaveFocus());
  });

  it('closes on «Cancelar» without changing the colour', async () => {
    const onChange = vi.fn();
    render(<Host onChange={onChange} />);
    const dialog = await openDialog();

    await userEvent.click(within(dialog).getByRole('button', { name: 'Cancelar' }));

    await waitFor(() => expect(screen.queryByTestId('catalog-color-dialog')).toBeNull());
    expect(onChange).not.toHaveBeenCalled();
  });

  it('closes without a change when the current colour is chosen again', async () => {
    const onChange = vi.fn();
    render(<Host initial="green" onChange={onChange} />);
    await openDialog();

    await userEvent.click(
      options().find((option) => option.getAttribute('data-value') === 'green')!,
    );

    await waitFor(() => expect(screen.queryByTestId('catalog-color-dialog')).toBeNull());
    expect(onChange).not.toHaveBeenCalled();
    expect(trigger()).toHaveAttribute('data-catalog-color', 'green');
  });
});
