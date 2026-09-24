import { act, render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { useState } from 'react';
import {
  CatalogValuePicker,
  type CatalogValuePickerProps,
} from '../../src/app/features/catalogs/components/catalog-value-picker';
import type { PickerItem } from '../../src/app/features/catalogs/components/catalog-value-picker.logic';

/**
 * The shared picker on its own. react-aria portals the option list and the chip editor to
 * `document.body`, so their content is queried through `screen`, never `container`.
 */
const LANGUAGES = ['Inglés', 'Francés', 'Alemán'];
const LEVELS = ['A1', 'B1', 'B2', 'C1'];

type HostProps = Partial<CatalogValuePickerProps> & { initial?: PickerItem[] };

function Host({ initial = [], onAdd, onChange, onRemove, ...props }: HostProps) {
  const [items, setItems] = useState<PickerItem[]>(initial);
  return (
    <CatalogValuePicker
      idPrefix="test-language"
      label="Idiomas"
      addLabel="Añadir idioma…"
      valueOptions={LANGUAGES}
      levelOptions={LEVELS}
      levelMode="optional"
      anyLevelLabel="Cualquier nivel"
      emptyText="Sin idiomas."
      {...props}
      items={items}
      onAdd={(item) => {
        onAdd?.(item);
        setItems((current) => [...current, item]);
      }}
      onChange={(item) => {
        onChange?.(item);
        setItems((current) => current.map((i) => (i.key === item.key ? item : i)));
      }}
      onRemove={(item) => {
        onRemove?.(item);
        setItems((current) => current.filter((i) => i.key !== item.key));
      }}
    />
  );
}

const input = () => screen.getByTestId('test-language-input');
const addButton = () => screen.getByTestId('test-language-add');
/** The input exists only while adding: (+) reveals it. */
const openInput = async () => {
  if (!screen.queryByTestId('test-language-input')) await userEvent.click(addButton());
  return input();
};
const chips = () => screen.queryAllByTestId('test-language-chip');
const chip = (value: string) =>
  chips().find((element) => element.getAttribute('data-value') === value) as HTMLElement;

describe('CatalogValuePicker', () => {
  describe('type-to-filter adding', () => {
    it('adds the highlighted value on Enter, clears the input and stops offering it', async () => {
      const onAdd = vi.fn();
      render(<Host onAdd={onAdd} />);

      await userEvent.type(await openInput(), 'ingl');
      expect(screen.getByRole('option', { name: 'Inglés' })).toBeInTheDocument();
      await userEvent.keyboard('{ArrowDown}{Enter}');

      expect(onAdd).toHaveBeenCalledWith(expect.objectContaining({ value: 'Inglés', level: '' }));
      expect(input()).toHaveValue('');
      expect(chip('Inglés')).toBeInTheDocument();
      // The list closes and focus stays in the input, ready for the next value.
      expect(screen.queryByRole('listbox')).toBeNull();
      expect(input()).toHaveFocus();

      await userEvent.type(await openInput(), 'i');
      expect(screen.queryByRole('option', { name: 'Inglés' })).not.toBeInTheDocument();
    });

    it('shows (+) at rest, which reveals the focused input with every value not held', async () => {
      render(<Host initial={[{ key: 'fr', value: 'Francés', level: '' }]} />);

      expect(screen.queryByTestId('test-language-input')).toBeNull();
      expect(screen.getByRole('button', { name: 'Añadir idioma…' })).toBe(addButton());
      await userEvent.click(addButton());

      expect(input()).toHaveFocus();
      const options = within(screen.getByRole('listbox')).getAllByRole('option');
      expect(options.map((option) => option.textContent)).toEqual(['Inglés', 'Alemán']);
      await userEvent.click(screen.getByRole('option', { name: 'Alemán' }));
      expect(chip('Alemán')).toBeInTheDocument();
      // Adding closes the list but keeps the input, focused, for the next value.
      expect(screen.queryByRole('listbox')).toBeNull();
      expect(input()).toHaveFocus();

      // A click on the input lists the rest again.
      await userEvent.click(input());
      expect(screen.getByRole('option', { name: 'Inglés' })).toBeInTheDocument();
    });

    it('goes back to (+) on a second Escape, or when focus moves elsewhere', async () => {
      render(
        <>
          <Host />
          <button type="button">Otro</button>
        </>,
      );

      await userEvent.click(addButton());
      await userEvent.keyboard('{Escape}');
      expect(screen.queryByRole('listbox')).toBeNull();
      expect(input()).toBeInTheDocument();
      await userEvent.keyboard('{Escape}');
      await waitFor(() => expect(addButton()).toHaveFocus());
      expect(screen.queryByTestId('test-language-input')).toBeNull();

      await userEvent.click(addButton());
      // By text: react-aria hides the rest of the page from assistive tech while the list is open.
      await userEvent.click(screen.getByText('Otro'));
      await waitFor(() => expect(screen.queryByTestId('test-language-input')).toBeNull());
      expect(screen.getByText('Otro')).toHaveFocus();
    });

    it('disables (+) when disabled', () => {
      render(<Host disabled />);

      expect(addButton()).toBeDisabled();
      expect(screen.queryByTestId('test-language-input')).toBeNull();
    });

    it('adds a value chosen with the pointer', async () => {
      render(<Host />);

      await userEvent.type(await openInput(), 'fr');
      await userEvent.click(screen.getByRole('option', { name: 'Francés' }));

      expect(chip('Francés')).toBeInTheDocument();
    });

    it('says so in Spanish when nothing matches, and adds nothing', async () => {
      const onAdd = vi.fn();
      render(<Host onAdd={onAdd} />);

      await userEvent.type(await openInput(), 'xyz');

      expect(screen.getByText('No hay coincidencias.')).toBeInTheDocument();
      await userEvent.keyboard('{Enter}');
      expect(onAdd).not.toHaveBeenCalled();
    });

    it('offers only the active values the host passes', async () => {
      render(<Host valueOptions={['Inglés']} />);

      await userEvent.type(await openInput(), 'a');

      expect(screen.queryByRole('option', { name: 'Alemán' })).not.toBeInTheDocument();
    });

    it('keeps the input name and test ids derived from the prefix', async () => {
      render(<Host />);

      expect(screen.getByTestId('test-language-picker')).toBeInTheDocument();
      await openInput();
      expect(input()).toHaveAttribute('name', 'test-language');
      expect(screen.getByRole('combobox', { name: 'Añadir idioma…' })).toBe(input());
    });
  });

  describe('chips', () => {
    const held: PickerItem[] = [
      { key: 'en', value: 'Inglés', level: '' },
      { key: 'old', value: 'Latín', level: 'B2' },
    ];

    it('shows value and level, with «Cualquier nivel» for an empty optional level', () => {
      render(<Host initial={held} />);

      expect(chip('Inglés')).toHaveTextContent('Cualquier nivel');
      expect(chip('Latín')).toHaveTextContent('B2');
    });

    it('still shows a value that is no longer active in the catalog, and removes it', async () => {
      const onRemove = vi.fn();
      render(<Host initial={held} onRemove={onRemove} />);

      await userEvent.click(screen.getByRole('button', { name: 'Quitar Latín' }));

      expect(onRemove).toHaveBeenCalledWith(expect.objectContaining({ key: 'old' }));
      expect(chip('Latín')).toBeUndefined();
    });

    it('removes a focused chip with Delete and offers its value again', async () => {
      render(<Host initial={held} />);

      act(() => chip('Inglés').focus());
      await userEvent.keyboard('{Delete}');

      expect(chip('Inglés')).toBeUndefined();
      await userEvent.type(await openInput(), 'ingl');
      expect(screen.getByRole('option', { name: 'Inglés' })).toBeInTheDocument();
    });

    it('shows the empty text only where nothing can be added', () => {
      const { unmount } = render(<Host />);
      expect(screen.queryByText('Sin idiomas.')).toBeNull();
      unmount();

      render(<Host readOnly />);
      expect(screen.getByText('Sin idiomas.')).toBeInTheDocument();
    });
  });

  describe('level editor', () => {
    it('changes the level in place, without a remove and re-add', async () => {
      const onChange = vi.fn();
      const onAdd = vi.fn();
      const onRemove = vi.fn();
      render(
        <Host
          initial={[{ key: 'en', value: 'Inglés', level: 'B1' }]}
          onChange={onChange}
          onAdd={onAdd}
          onRemove={onRemove}
        />,
      );

      await userEvent.click(chip('Inglés'));
      const editor = await screen.findByTestId('test-language-editor');
      await userEvent.click(within(editor).getByRole('radio', { name: 'C1' }));

      expect(onChange).toHaveBeenCalledWith({ key: 'en', value: 'Inglés', level: 'C1' });
      expect(onAdd).not.toHaveBeenCalled();
      expect(onRemove).not.toHaveBeenCalled();
      expect(chip('Inglés')).toHaveTextContent('C1');
      await waitFor(() => expect(screen.queryByTestId('test-language-editor')).toBeNull());
    });

    it('offers «Cualquier nivel» first for an optional level, then the levels in order', async () => {
      render(<Host initial={[{ key: 'en', value: 'Inglés', level: 'B1' }]} />);

      await userEvent.click(chip('Inglés'));
      const group = within(await screen.findByRole('radiogroup', { name: 'Nivel' }));

      expect(group.getAllByRole('radio').map((radio) => radio.textContent)).toEqual([
        'Cualquier nivel',
        ...LEVELS,
      ]);
      await userEvent.click(group.getByRole('radio', { name: 'Cualquier nivel' }));
      expect(chip('Inglés')).toHaveTextContent('Cualquier nivel');
    });

    it('falls back to a select for a long level family', async () => {
      const levels = ['A1', 'A2', 'B1', 'B2', 'C1', 'C2', 'Nativo'];
      render(
        <Host levelOptions={levels} initial={[{ key: 'en', value: 'Inglés', level: 'B1' }]} />,
      );

      await userEvent.click(chip('Inglés'));
      await screen.findByTestId('test-language-editor');

      expect(screen.queryByRole('radiogroup')).toBeNull();
      await userEvent.click(screen.getByRole('button', { name: /B1/ }));
      await userEvent.click(await screen.findByRole('option', { name: 'Nativo' }));
      expect(chip('Inglés')).toHaveTextContent('Nativo');
    });

    it('opens no editor for a family without levels', async () => {
      render(
        <Host
          idPrefix="test-language"
          levelOptions={undefined}
          initial={[{ key: 'vip', value: 'VIP', level: '' }]}
        />,
      );

      await userEvent.click(chip('VIP'));

      expect(screen.queryByTestId('test-language-editor')).toBeNull();
      expect(chip('VIP')).toHaveTextContent('VIP');
      expect(chip('VIP')).not.toHaveTextContent('Cualquier nivel');
    });

    it('commits a detail when focus leaves it, keeping the level', async () => {
      const onChange = vi.fn();
      render(
        <Host
          initial={[{ key: 'en', value: 'Inglés', level: 'B1', details: { certification: '' } }]}
          onChange={onChange}
          renderDetails={(draft, set) => (
            <label>
              Certificación
              <input
                name="certification"
                value={String(draft.details?.['certification'] ?? '')}
                onChange={(e) =>
                  set({ ...draft, details: { ...draft.details, certification: e.target.value } })
                }
              />
            </label>
          )}
        />,
      );

      await userEvent.click(chip('Inglés'));
      await userEvent.type(await screen.findByLabelText('Certificación'), 'TOEFL');
      await userEvent.tab();

      expect(onChange).toHaveBeenLastCalledWith({
        key: 'en',
        value: 'Inglés',
        level: 'B1',
        details: { certification: 'TOEFL' },
      });
    });
  });

  describe('required level', () => {
    it('commits at once with the lowest level and opens no editor', async () => {
      const onAdd = vi.fn();
      render(<Host levelMode="required" onAdd={onAdd} />);

      await userEvent.type(await openInput(), 'ale');
      await userEvent.keyboard('{ArrowDown}{Enter}');

      expect(onAdd).toHaveBeenCalledTimes(1);
      expect(onAdd).toHaveBeenCalledWith(expect.objectContaining({ value: 'Alemán', level: 'A1' }));
      expect(chip('Alemán')).toHaveTextContent('A1');
      expect(screen.queryByTestId('test-language-editor')).toBeNull();
      await waitFor(() => expect(input()).toHaveFocus());
    });

    it('changes the default level on the chip, offering no «Cualquier nivel»', async () => {
      const onChange = vi.fn();
      render(
        <Host
          levelMode="required"
          initial={[{ key: 'de', value: 'Alemán', level: 'A1' }]}
          onChange={onChange}
        />,
      );

      await userEvent.click(chip('Alemán'));
      const editor = await screen.findByTestId('test-language-editor');
      expect(within(editor).queryByRole('radio', { name: 'Cualquier nivel' })).toBeNull();
      await userEvent.click(within(editor).getByRole('radio', { name: 'B2' }));

      expect(onChange).toHaveBeenCalledWith(expect.objectContaining({ key: 'de', level: 'B2' }));
      expect(chip('Alemán')).toHaveTextContent('B2');
    });

    it('keeps the default level when the editor is closed without a choice', async () => {
      const onChange = vi.fn();
      render(
        <Host
          levelMode="required"
          initial={[{ key: 'de', value: 'Alemán', level: 'A1' }]}
          onChange={onChange}
        />,
      );

      await userEvent.click(chip('Alemán'));
      await screen.findByTestId('test-language-editor');
      await userEvent.keyboard('{Escape}');

      await waitFor(() => expect(screen.queryByTestId('test-language-editor')).toBeNull());
      expect(onChange).not.toHaveBeenCalled();
      expect(chip('Alemán')).toHaveTextContent('A1');
    });

    it('cannot add while the level family offers no level, but still removes', async () => {
      const onRemove = vi.fn();
      render(
        <Host
          levelMode="required"
          levelOptions={[]}
          initial={[{ key: 'de', value: 'Alemán', level: 'B1' }]}
          onRemove={onRemove}
        />,
      );

      expect(addButton()).toBeDisabled();
      await userEvent.click(within(chip('Alemán')).getByTestId('test-language-remove'));
      expect(onRemove).toHaveBeenCalledWith(expect.objectContaining({ key: 'de' }));
    });
  });

  describe('asynchronous commits', () => {
    it('marks a pending chip', () => {
      render(<Host initial={[{ key: 'en', value: 'Inglés', level: 'B1', status: 'pending' }]} />);

      expect(chip('Inglés')).toHaveAttribute('data-status', 'pending');
      expect(chip('Inglés')).toHaveTextContent('Guardando…');
    });

    it('takes no edit or removal on a pending chip until it settles', async () => {
      const onChange = vi.fn();
      const onRemove = vi.fn();
      render(
        <Host
          initial={[{ key: 'en', value: 'Inglés', level: 'B1', status: 'pending' }]}
          onChange={onChange}
          onRemove={onRemove}
        />,
      );

      await userEvent.click(chip('Inglés'));
      expect(screen.queryByTestId('test-language-editor')).toBeNull();
      await userEvent.click(within(chip('Inglés')).getByTestId('test-language-remove'));
      chip('Inglés').focus();
      await userEvent.keyboard('{Delete}');

      expect(onChange).not.toHaveBeenCalled();
      expect(onRemove).not.toHaveBeenCalled();
      expect(chip('Inglés')).toBeInTheDocument();
    });

    it('shows a failed chip with its message and a retry, leaving the others alone', async () => {
      const onRetry = vi.fn();
      const failed: PickerItem = {
        key: 'en',
        value: 'Inglés',
        level: 'B1',
        status: 'error',
        error: 'Otro usuario ha cambiado el candidato.',
      };
      render(
        <Host initial={[failed, { key: 'fr', value: 'Francés', level: 'A1' }]} onRetry={onRetry} />,
      );

      expect(chip('Inglés')).toHaveAttribute('data-status', 'error');
      expect(chip('Francés')).not.toHaveAttribute('data-status');
      expect(screen.getByRole('alert')).toHaveTextContent(
        'Inglés: Otro usuario ha cambiado el candidato.',
      );
      await userEvent.click(screen.getByRole('button', { name: 'Reintentar Inglés' }));
      expect(onRetry).toHaveBeenCalledWith(failed);
    });
  });

  describe('read-only and disabled', () => {
    const held: PickerItem[] = [{ key: 'en', value: 'Inglés', level: 'B1' }];

    it('shows only the chips when read-only', async () => {
      render(<Host initial={held} readOnly />);

      expect(chip('Inglés')).toHaveTextContent('B1');
      expect(screen.queryByRole('combobox')).toBeNull();
      expect(screen.queryByRole('button')).toBeNull();
      await userEvent.click(chip('Inglés'));
      expect(screen.queryByTestId('test-language-editor')).toBeNull();
    });

    it('keeps the chips and disables adding when disabled', () => {
      render(<Host initial={held} disabled />);

      expect(chip('Inglés')).toBeInTheDocument();
      expect(addButton()).toBeDisabled();
    });
  });

  it('is operable by keyboard alone: add, change the level and remove', async () => {
    const user = userEvent.setup();
    render(<Host />);

    await user.tab();
    expect(addButton()).toHaveFocus();
    await user.keyboard('{Enter}');
    await waitFor(() => expect(input()).toHaveFocus());
    await user.keyboard('fra');
    await user.keyboard('{ArrowDown}{Enter}');
    expect(chip('Francés')).toHaveTextContent('Cualquier nivel');

    // Back past the chip's remove button to the chip itself, and open its editor.
    await user.tab({ shift: true });
    expect(screen.getByRole('button', { name: 'Quitar Francés' })).toHaveFocus();
    await user.tab({ shift: true });
    expect(chip('Francés')).toHaveFocus();
    await user.keyboard('{Enter}');
    await screen.findByTestId('test-language-editor');
    await waitFor(() =>
      expect(screen.getByRole('radio', { name: 'Cualquier nivel' })).toHaveFocus(),
    );
    await user.keyboard('{ArrowRight}{ArrowRight} ');
    expect(chip('Francés')).toHaveTextContent('B1');
    await waitFor(() => expect(chip('Francés')).toHaveFocus());

    await user.keyboard('{Backspace}');
    expect(chips()).toHaveLength(0);
  });
});
