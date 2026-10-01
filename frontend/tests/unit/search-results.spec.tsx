import { fireEvent, render, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter, Route, Routes } from 'react-router';
import { services, type Services } from '../../src/app/core/di/services';
import { ServicesProvider } from '../../src/app/core/di/services-context';
import { signal } from '../../src/app/core/state/signal';
import { RowCvPreviewProvider } from '../../src/app/features/candidates/components/row-cv-preview/row-cv-preview';
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
  availabilityState: 'available',
  availabilityCheckedOn: '2026-09-20',
  hasPrimaryCv: true,
  primaryCvPreviewable: true,
  primaryCvDownloadable: true,
  updatedAt: '2026-09-20T09:00:00Z',
  isActive: true,
};
const onePage: SearchResultPage = { items: [result], page: 1, pageSize: 25, totalCount: 1 };

describe('SearchResults (KTL-30)', () => {
  const documentService = {
    list: vi.fn(),
    download: vi.fn(),
    openPreview: vi.fn(),
    observeUntilSettled: vi.fn(),
  };
  const toastService = { show: vi.fn() };
  beforeEach(() => {
    vi.clearAllMocks();
    documentService.list.mockResolvedValue([]);
  });

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
            documentService,
            toastService,
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
                <RowCvPreviewProvider>
                  <SearchResults
                    results={results}
                    loading={false}
                    failed={false}
                    lastPage={Math.max(1, Math.ceil(results.totalCount / results.pageSize))}
                    onPageChange={() => undefined}
                    renderRowAction={renderRowAction}
                  />
                </RowCvPreviewProvider>
              }
            />
            <Route path="/app/candidates/:id" element={<p data-testid="candidate-page" />} />
          </Routes>
        </MemoryRouter>
      </ServicesProvider>,
    );

  it('renders the same Spanish copy as before, from the catalogue', () => {
    renderResults(onePage, undefined, []);

    for (const header of ['Candidato', 'Teléfono', 'Disponibilidad', 'Actualizado'])
      expect(screen.getByRole('columnheader', { name: header })).toBeInTheDocument();
    expect(screen.queryByText('Disponible')).toBeNull();
    expect(screen.getByTestId('search-total')).toHaveTextContent(
      '1 candidato encontrado · Página 1 de 1',
    );
    expect(screen.getByRole('button', { name: 'Anterior' })).toBeDisabled();
    expect(screen.getByRole('button', { name: 'Siguiente' })).toBeDisabled();
  });

  it('has no CV column, «Ver» or «Abrir CV» without the download permission (KTL-35)', () => {
    renderResults(onePage, undefined, []);

    expect(screen.queryByRole('columnheader', { name: 'CV' })).toBeNull();
    expect(screen.queryByTestId('row-cv-toggle')).toBeNull();
    expect(screen.queryByRole('button', { name: 'Abrir CV' })).toBeNull();
    expect(screen.queryByRole('img', { name: 'Con CV' })).toBeNull();
  });

  it('ends with a «CV» column offering «Ver» only for a previewable CV (KTL-35)', () => {
    renderResults({
      ...onePage,
      items: [result, { ...result, candidateId: 'c-2', primaryCvPreviewable: false }],
      totalCount: 2,
    });

    const headers = screen.getAllByRole('columnheader');
    expect(headers.at(-1)).toHaveTextContent('CV');
    const [first, second] = screen.getAllByRole('row').slice(1);
    expect(
      within(first).getByRole('button', { name: 'Ver el CV de Ana García' }),
    ).toHaveTextContent('↓');
    expect(within(second).queryByTestId('row-cv-toggle')).toBeNull();
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
    await userEvent.click(screen.getByTestId('row-cv-toggle'));

    expect(screen.queryByTestId('candidate-page')).toBeNull();
  });

  it('ignores a near-miss next to the row action or the CV buttons (KTL-35)', async () => {
    renderResults(onePage, () => (
      <button type="button" data-testid="row-action">
        Añadir
      </button>
    ));
    const cells = within(screen.getAllByRole('row')[1]).getAllByRole('cell');

    await userEvent.click(cells.at(-2)!);
    await userEvent.click(cells.at(-1)!);

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

  it('renders the row action in its own cell, before the «CV» column', () => {
    renderResults(onePage, (item) => (
      <button type="button" data-testid="row-action">
        {item.candidateId}
      </button>
    ));

    const action = screen.getByTestId('row-action');
    expect(action).toHaveTextContent('c-1');
    expect(within(action.parentElement!).getAllByRole('button')).toHaveLength(1);
    const cells = within(screen.getAllByRole('row')[1]).getAllByRole('cell');
    expect(cells.at(-2)).toContainElement(action);
    expect(cells.at(-1)).toContainElement(screen.getByTestId('row-cv-toggle'));
  });

  it('offers a download, left of «Ver», and only a download for a CV that cannot be previewed (KTL-35)', async () => {
    documentService.list.mockResolvedValue([
      {
        id: 'doc-1',
        isPrimary: true,
        availabilityState: 'Available',
        originalFilename: 'cv.docx',
        mimeType: 'application/vnd.openxmlformats-officedocument.wordprocessingml.document',
      },
    ]);
    renderResults({
      ...onePage,
      items: [
        result,
        { ...result, candidateId: 'c-2', lastName: 'Word', primaryCvPreviewable: false },
        {
          ...result,
          candidateId: 'c-3',
          lastName: 'Pendiente',
          primaryCvPreviewable: false,
          primaryCvDownloadable: false,
        },
      ],
      totalCount: 3,
    });
    const [pdfRow, wordRow, pendingRow] = screen.getAllByRole('row').slice(1);

    const pdfButtons = within(pdfRow).getAllByRole('button');
    expect(pdfButtons.map((button) => button.getAttribute('data-testid'))).toEqual([
      'row-cv-download',
      'row-cv-toggle',
    ]);
    expect(within(wordRow).queryByTestId('row-cv-toggle')).toBeNull();
    expect(within(pendingRow).queryAllByRole('button')).toHaveLength(0);

    await userEvent.click(
      within(wordRow).getByRole('button', { name: 'Descargar el CV de Ana Word' }),
    );

    expect(documentService.list).toHaveBeenCalledWith('c-2');
    expect(documentService.download).toHaveBeenCalledWith('c-2', 'doc-1', 'cv.docx');
    expect(screen.queryByTestId('candidate-page')).toBeNull();
  });

  it('warns instead of downloading when the CV is no longer available', async () => {
    documentService.list.mockResolvedValue([]);
    renderResults(onePage);

    await userEvent.click(screen.getByTestId('row-cv-download'));

    expect(documentService.download).not.toHaveBeenCalled();
    expect(toastService.show).toHaveBeenCalledWith(
      'El CV de este candidato ya no se puede descargar.',
      'warning',
    );
  });
});
