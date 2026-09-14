import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { useState } from 'react';
import { SearchCriteriaDialog } from '../../src/app/features/search/components/search-criteria-dialog';
import { EMPTY_SEARCH_FILTERS } from '../../src/app/features/search/models/search.models';

const filters = { ...structuredClone(EMPTY_SEARCH_FILTERS), text: 'Marta', hasCv: 'yes' as const };

/** A host with an opener button, so focus restoration has somewhere to return to. */
function Host({ onClose = () => undefined }: { onClose?: () => void }) {
  const [open, setOpen] = useState(false);
  return (
    <>
      <button type="button" onClick={() => setOpen(true)}>
        abrir
      </button>
      {open ? (
        <SearchCriteriaDialog
          title="Java senior"
          filters={filters}
          details={<p>detalle del host</p>}
          actions={<a href="/editar">editar</a>}
          onClose={() => {
            onClose();
            setOpen(false);
          }}
        />
      ) : null}
    </>
  );
}

describe('SearchCriteriaDialog', () => {
  it('shows the title, the host details, the shared summary and the host actions', async () => {
    render(<Host />);
    await userEvent.click(screen.getByRole('button', { name: 'abrir' }));

    const dialog = screen.getByRole('dialog', { name: 'Java senior' });
    expect(dialog).toHaveAttribute('aria-modal', 'true');
    expect(dialog).toHaveTextContent('detalle del host');
    expect(screen.getByTestId('filters-summary')).toHaveTextContent('Marta');
    expect(screen.getByTestId('filters-summary')).toHaveTextContent('Con CV');
    expect(screen.getByRole('link', { name: 'editar' })).toBeInTheDocument();
  });

  it('moves focus into the dialog and returns it to the opener on close', async () => {
    render(<Host />);
    const opener = screen.getByRole('button', { name: 'abrir' });
    await userEvent.click(opener);

    expect(screen.getByTestId('criteria-dialog-close')).toHaveFocus();

    await userEvent.click(screen.getByTestId('criteria-dialog-close'));

    expect(screen.queryByRole('dialog')).toBeNull();
    expect(opener).toHaveFocus();
  });

  it('closes on Escape and on a click outside, but not on a click inside', async () => {
    const onClose = vi.fn();
    render(<Host onClose={onClose} />);
    await userEvent.click(screen.getByRole('button', { name: 'abrir' }));

    await userEvent.click(screen.getByRole('heading', { name: 'Java senior' }));
    expect(onClose).not.toHaveBeenCalled();

    await userEvent.keyboard('{Escape}');
    expect(onClose).toHaveBeenCalledTimes(1);

    await userEvent.click(screen.getByRole('button', { name: 'abrir' }));
    await userEvent.click(screen.getByTestId('criteria-dialog-overlay'));
    expect(onClose).toHaveBeenCalledTimes(2);
  });

  it('keeps Tab within the dialog', async () => {
    render(<Host />);
    await userEvent.click(screen.getByRole('button', { name: 'abrir' }));
    const close = screen.getByTestId('criteria-dialog-close');
    const edit = screen.getByRole('link', { name: 'editar' });

    // Close is last, so Tab wraps to the first focusable element, and Shift+Tab wraps back.
    await userEvent.tab();
    expect(edit).toHaveFocus();
    await userEvent.tab({ shift: true });
    expect(close).toHaveFocus();
  });
});
