import { fireEvent, render, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter, Route, Routes } from 'react-router';
import { services, type Services } from '../../src/app/core/di/services';
import { ServicesProvider } from '../../src/app/core/di/services-context';
import { signal } from '../../src/app/core/state/signal';
import { SearchResults } from '../../src/app/features/search/components/search-results';
import type {
  SearchResult,
  SearchResultPage,
} from '../../src/app/features/search/models/search.models';

const result: SearchResult = {
  candidateId: 'c-1',
  firstName: 'Ana',
  lastName: 'García',
  phone: '600000000',
  email: 'ana@example.test',
  status: 'in_process',
  hasPrimaryCv: true,
  primaryCvDocumentId: 'd-1',
  updatedAt: '2026-09-20T09:00:00Z',
  isActive: true,
};
const onePage: SearchResultPage = { items: [result], page: 1, pageSize: 25, totalCount: 1 };

describe('SearchResults (KTL-30)', () => {
  const renderResults = (
    results: SearchResultPage,
    renderRowAction?: (item: SearchResult) => React.ReactNode,
    granted: string[] = ['documents.download'],
  ) =>
    render(
      <ServicesProvider
        value={
          {
            ...services,
            authService: {
              profile: signal(null),
              hasPermission: (permission: string) => granted.includes(permission),
            },
          } as unknown as Services
        }
      >
        <MemoryRouter initialEntries={['/search']}>
          <Routes>
            <Route
              path="/search"
              element={
                <SearchResults
                  results={results}
                  loading={false}
                  failed={false}
                  lastPage={Math.max(1, Math.ceil(results.totalCount / results.pageSize))}
                  onPageChange={() => undefined}
                  renderRowAction={renderRowAction}
                />
              }
            />
            <Route path="/app/candidates/:id" element={<p data-testid="candidate-page" />} />
          </Routes>
        </MemoryRouter>
      </ServicesProvider>,
    );

  it('renders the same Spanish copy as before, from the catalogue', () => {
    renderResults(onePage, undefined, []);

    for (const header of ['Candidato', 'Teléfono', 'Estado', 'CV', 'Actualizado'])
      expect(screen.getByRole('columnheader', { name: header })).toBeInTheDocument();
    expect(screen.getByText('Disponible')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Abrir CV' })).toBeDisabled();
    expect(screen.getByTestId('search-total')).toHaveTextContent(
      '1 candidato encontrado · Página 1 de 1',
    );
    expect(screen.getByText('Tu rol no permite abrir CVs desde resultados.')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Anterior' })).toBeDisabled();
    expect(screen.getByRole('button', { name: 'Siguiente' })).toBeDisabled();
  });

  it('keeps the empty state and zero count unchanged', () => {
    renderResults({ items: [], page: 1, pageSize: 25, totalCount: 0 });

    expect(screen.getByTestId('search-empty')).toHaveTextContent('Sin resultados.');
    expect(screen.getByTestId('search-total')).toHaveTextContent(/^0 candidatos encontrados$/);
  });

  it('links the name, keeps the e-mail as mailto and shows the phone as plain text', () => {
    renderResults(onePage);

    expect(screen.getByRole('link', { name: 'Ana García' })).toHaveAttribute(
      'href',
      '/app/candidates/c-1',
    );
    expect(screen.getByRole('link', { name: 'ana@example.test' })).toHaveAttribute(
      'href',
      'mailto:ana@example.test',
    );
    expect(screen.getByText('600000000').closest('a')).toBeNull();
    expect(screen.queryByRole('link', { name: 'Detalle' })).toBeNull();
  });

  it('opens the candidate when any plain part of the row is clicked', async () => {
    renderResults(onePage);

    await userEvent.click(screen.getByText('600000000'));

    expect(screen.getByTestId('candidate-page')).toBeInTheDocument();
  });

  it('does not navigate when a control inside the row is clicked', async () => {
    renderResults(onePage, () => (
      <button type="button" data-testid="row-action">
        Añadir
      </button>
    ));

    await userEvent.click(screen.getByTestId('row-action'));
    await userEvent.click(screen.getByRole('button', { name: 'Abrir CV' }));

    expect(screen.queryByTestId('candidate-page')).toBeNull();
  });

  it('opens a new tab on Ctrl-click and middle-click instead of navigating', () => {
    const open = vi.spyOn(window, 'open').mockReturnValue(null);
    renderResults(onePage);
    const phone = screen.getByText('600000000');

    fireEvent.click(phone, { ctrlKey: true });
    fireEvent(phone, new MouseEvent('auxclick', { bubbles: true, button: 1 }));

    expect(open).toHaveBeenCalledTimes(2);
    expect(open).toHaveBeenCalledWith('/app/candidates/c-1', '_blank', 'noopener');
    expect(screen.queryByTestId('candidate-page')).toBeNull();
    open.mockRestore();
  });

  it('renders the row action first in the actions cell', () => {
    renderResults(onePage, (item) => (
      <button type="button" data-testid="row-action">
        {item.candidateId}
      </button>
    ));

    const action = screen.getByTestId('row-action');
    expect(action).toHaveTextContent('c-1');
    expect(action.nextElementSibling).toBe(screen.getByRole('button', { name: 'Abrir CV' }));
    expect(within(action.parentElement!).getAllByRole('button')).toHaveLength(2);
  });
});
