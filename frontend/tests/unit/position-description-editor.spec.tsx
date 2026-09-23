import { act, render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import {
  $createParagraphNode,
  $createTextNode,
  $getRoot,
  PASTE_COMMAND,
  type LexicalEditor,
} from 'lexical';
import {
  PositionDescription,
  PositionDescriptionEditor,
} from '../../src/app/features/positions/components/position-description-editor';

const editorInput = () => screen.getByRole('textbox', { name: 'Descripción' });
const lexicalEditor = () =>
  (editorInput() as HTMLElement & { __lexicalEditor: LexicalEditor }).__lexicalEditor;

/** Pastes through the editor's own paste command at the end of the content. */
function paste(editor: LexicalEditor, html: string, text: string) {
  const data = new DataTransfer();
  data.setData('text/html', html);
  data.setData('text/plain', text);
  const event = new ClipboardEvent('paste', { bubbles: true, cancelable: true });
  Object.defineProperty(event, 'clipboardData', { value: data });
  editor.update(() => $getRoot().selectEnd(), { discrete: true });
  editor.dispatchCommand(PASTE_COMMAND, event);
}

describe('PositionDescriptionEditor', () => {
  beforeAll(() => {
    // Lexical's rich-text plugin checks `instanceof DragEvent`, which jsdom does not define.
    if (typeof globalThis.DragEvent === 'undefined') {
      (globalThis as { DragEvent?: unknown }).DragEvent = class DragEvent extends Event {};
    }
    if (typeof globalThis.ClipboardEvent === 'undefined') {
      (globalThis as { ClipboardEvent?: unknown }).ClipboardEvent = class ClipboardEvent extends (
        Event
      ) {};
    }
    // jsdom has no DataTransfer; Lexical only needs getData/types for paste.
    if (typeof globalThis.DataTransfer === 'undefined') {
      class MemoryDataTransfer {
        private readonly values = new Map<string, string>();
        readonly files: File[] = [];
        readonly items: unknown[] = [];
        get types() {
          return [...this.values.keys()];
        }
        setData(type: string, value: string) {
          this.values.set(type, value);
        }
        getData(type: string) {
          return this.values.get(type) ?? '';
        }
      }
      (globalThis as { DataTransfer?: unknown }).DataTransfer = MemoryDataTransfer;
    }
  });

  it('is a labelled, named editor with exactly the approved formatting controls', () => {
    render(<PositionDescriptionEditor value="" onChange={vi.fn()} />);

    expect(editorInput()).toHaveAttribute('name', 'description');
    expect(editorInput()).toHaveAttribute('contenteditable', 'true');
    const toolbar = screen.getByRole('toolbar', { name: 'Formato de la descripción' });
    expect(
      within(toolbar)
        .getAllByRole('button')
        .map((button) => button.textContent),
    ).toEqual(['Negrita', 'Cursiva', 'Lista con viñetas', 'Lista numerada']);
    // No link, image, heading, colour or embed control is offered.
    expect(
      within(toolbar).queryByRole('button', { name: /enlace|imagen|título|color/i }),
    ).toBeNull();
    expect(screen.getByText('Describe la posición…')).toBeInTheDocument();
  });

  it('renders an initial value with its structure', async () => {
    render(
      <PositionDescriptionEditor
        value="<p>Hola <b>equipo</b></p><ul><li>Uno</li><li>Dos</li></ul>"
        onChange={vi.fn()}
      />,
    );

    await waitFor(() => expect(editorInput()).toHaveTextContent('Hola equipo'));
    expect(within(editorInput()).getAllByRole('listitem')).toHaveLength(2);
    expect(screen.queryByText('Describe la posición…')).not.toBeInTheDocument();
  });

  // jsdom does not emit the beforeinput events Lexical types through, so text is written with
  // the editor's own API; toolbar commands and paste still go through the rendered controls.
  const typeText = (text: string) => {
    const editor = lexicalEditor();
    act(() =>
      editor.update(
        () => {
          const paragraph = $createParagraphNode();
          paragraph.append($createTextNode(text));
          $getRoot().clear().append(paragraph);
          paragraph.select(0, text.length);
        },
        { discrete: true },
      ),
    );
  };

  it('propagates text and list formatting as HTML', async () => {
    const onChange = vi.fn();
    render(<PositionDescriptionEditor value="" onChange={onChange} />);

    typeText('Remoto');
    await userEvent.click(screen.getByRole('button', { name: 'Lista con viñetas' }));
    await waitFor(() =>
      expect(onChange).toHaveBeenLastCalledWith(
        expect.stringMatching(/<ul[^>]*>.*<li[^>]*>.*Remoto/),
      ),
    );

    await userEvent.click(screen.getByRole('button', { name: 'Lista numerada' }));
    await waitFor(() =>
      expect(onChange).toHaveBeenLastCalledWith(expect.stringMatching(/<ol[^>]*>/)),
    );
  });

  it.each([
    ['Negrita', /<(b|strong)[^>]*>.*fuerte/],
    ['Cursiva', /<(i|em)[^>]*>.*fuerte/],
  ])('applies %s from the toolbar to the selection', async (control, expected) => {
    const onChange = vi.fn();
    render(<PositionDescriptionEditor value="" onChange={onChange} />);
    typeText('fuerte');

    await userEvent.click(screen.getByRole('button', { name: control }));

    await waitFor(() => expect(onChange).toHaveBeenLastCalledWith(expect.stringMatching(expected)));
  });

  it('keeps pasted text but drops links, images and scripts it cannot represent', async () => {
    const onChange = vi.fn();
    render(<PositionDescriptionEditor value="" onChange={onChange} />);
    typeText('x');

    act(() =>
      paste(
        lexicalEditor(),
        '<p>Ver <a href="https://example.test">oferta</a><img src="x" onerror="alert(1)"><script>alert(2)</script></p>',
        'Ver oferta',
      ),
    );

    await waitFor(() =>
      expect(onChange).toHaveBeenLastCalledWith(expect.stringContaining('oferta')),
    );
    const html = onChange.mock.lastCall![0] as string;
    expect(html).not.toMatch(/<a\b|<img\b|<script\b|onerror|href/i);
  });

  it('can be reached and typed into with the keyboard alone', async () => {
    render(<PositionDescriptionEditor value="" onChange={vi.fn()} />);

    await userEvent.tab();
    expect(screen.getByRole('button', { name: 'Negrita' })).toHaveFocus();
    for (let index = 0; index < 4; index++) await userEvent.tab();

    expect(editorInput()).toHaveFocus();
  });
});

describe('PositionDescription', () => {
  it('renders canonical list semantics without inventing headings', () => {
    render(<PositionDescription html="<p>Texto</p><ol><li>Uno</li></ol>" />);

    const description = screen.getByTestId('position-description');
    expect(within(description).getByRole('list')).toBeInTheDocument();
    expect(within(description).getByRole('listitem')).toHaveTextContent('Uno');
    expect(within(description).queryByRole('heading')).toBeNull();
  });

  it('offers no expand control for a short description', () => {
    render(<PositionDescription html="<p>Corta</p>" />);

    expect(screen.queryByRole('button', { name: 'Ver más' })).toBeNull();
  });

  it('expands and collapses a long description accessibly', async () => {
    const height = vi.spyOn(HTMLElement.prototype, 'scrollHeight', 'get').mockReturnValue(1000);
    const scroll = vi.fn();
    HTMLElement.prototype.scrollIntoView = scroll;
    try {
      render(<PositionDescription html="<p>Larga</p>" />);

      const toggle = await screen.findByRole('button', { name: 'Ver más' });
      expect(toggle).toHaveAttribute('aria-expanded', 'false');
      await userEvent.click(toggle);
      expect(screen.getByRole('button', { name: 'Ver menos' })).toHaveAttribute(
        'aria-expanded',
        'true',
      );
      await userEvent.click(screen.getByRole('button', { name: 'Ver menos' }));
      expect(screen.getByRole('button', { name: 'Ver más' })).toHaveAttribute(
        'aria-expanded',
        'false',
      );
    } finally {
      height.mockRestore();
    }
  });
});
