import { act, fireEvent, render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter, useLocation } from 'react-router';
import { services, type Services } from '../../src/app/core/di/services';
import { ServicesProvider } from '../../src/app/core/di/services-context';
import { signal } from '../../src/app/core/state/signal';
import type { Candidate } from '../../src/app/features/candidates/models/candidate.models';
import { CandidateListPage } from '../../src/app/features/candidates/pages/candidate-list-page';
import { CandidateService } from '../../src/app/features/candidates/services/candidate.service';
import { createCandidateTestBed, FakeCandidateApi } from './support/candidate-doubles';

/** Seconds apart, so the default update-time ordering is unambiguous. */
function seedCandidates(api: FakeCandidateApi, count: number, patch: Partial<Candidate> = {}) {
  for (let index = 0; index < count; index += 1) {
    api.seed({
      id: `c-${String(index).padStart(3, '0')}`,
      firstName: `Nombre${index}`,
      lastName: `Apellido${String(index).padStart(3, '0')}`,
      status: index % 2 ? 'available' : 'new',
      updatedAt: new Date(Date.UTC(2026, 0, 1, 0, 0, index)).toISOString(),
      ...patch,
    });
  }
}

let currentSearch = '';
let currentPath = '';
function LocationProbe() {
  const location = useLocation();
  currentSearch = location.search;
  currentPath = location.pathname;
  return null;
}

describe('CandidateListPage', () => {
  let candidateService: CandidateService;
  let api: FakeCandidateApi;
  const toastService = { show: vi.fn() };
  const confirmDialogService = { confirm: vi.fn().mockResolvedValue(true) };
  let granted: string[];
  // usePermission subscribes to the profile signal before reading, so the double carries one.
  const authService = {
    profile: signal<unknown>({ id: 'u-1', isActive: true }),
    hasPermission: vi.fn((permission: string) => granted.includes(permission)),
  };

  const renderPage = (url = '/app/candidates') =>
    render(
      <ServicesProvider
        value={
          {
            ...services,
            candidateService,
            authService,
            toastService,
            confirmDialogService,
          } as unknown as Services
        }
      >
        <MemoryRouter initialEntries={[url]}>
          <CandidateListPage />
          <LocationProbe />
        </MemoryRouter>
      </ServicesProvider>,
    );

  const rows = () => screen.queryAllByTestId('candidate-row');
  const selectAll = () => within(screen.getByRole('table')).getAllByRole('checkbox')[0];
  const settle = () => waitFor(() => expect(screen.queryByText('Cargando candidatos…')).toBeNull());

  beforeEach(() => {
    toastService.show.mockClear();
    confirmDialogService.confirm.mockClear();
    granted = ['candidates.read', 'candidates.update', 'candidates.create', 'candidates.delete'];
    ({ service: candidateService, api } = createCandidateTestBed());
  });

  it('issues one request for the default view and renders the server total', async () => {
    seedCandidates(api, 30);
    renderPage();

    await waitFor(() => expect(rows()).toHaveLength(25));
    expect(api.listQueries).toHaveLength(1);
    expect(api.listQueries[0]).toMatchObject({
      page: 1,
      pageSize: 25,
      sortField: 'updatedAt',
      sortDirection: 'desc',
      includeInactive: false,
    });
    expect(screen.getByTestId('candidate-list-total')).toHaveTextContent('Mostrando 25 de 30');
    expect(currentSearch).toBe('');
  });

  it('renders no breadcrumb, being a top-level destination (KTL-23)', async () => {
    seedCandidates(api, 1);
    renderPage();

    await waitFor(() => expect(rows()).toHaveLength(1));
    expect(screen.queryByTestId('breadcrumb')).not.toBeInTheDocument();
  });

  it('pages through the server and writes the page to the URL', async () => {
    seedCandidates(api, 30);
    renderPage();
    await waitFor(() => expect(rows()).toHaveLength(25));

    await userEvent.click(screen.getByRole('button', { name: 'Siguiente' }));

    await waitFor(() => expect(rows()).toHaveLength(5));
    expect(api.listQueries.at(-1)?.page).toBe(2);
    expect(currentSearch).toBe('?page=2');
  });

  it('sorts on the server across the whole set rather than reordering the page', async () => {
    seedCandidates(api, 30);
    renderPage();
    await waitFor(() => expect(rows()).toHaveLength(25));

    await userEvent.click(screen.getByTestId('candidate-sort-lastName'));

    await waitFor(() => expect(rows()[0]).toHaveTextContent('Apellido000'));
    expect(api.listQueries.at(-1)).toMatchObject({
      sortField: 'lastName',
      sortDirection: 'asc',
      page: 1,
    });
    expect(currentSearch).toBe('?sort=lastName');
  });

  it('filters on the server', async () => {
    seedCandidates(api, 30);
    renderPage();
    await waitFor(() => expect(rows()).toHaveLength(25));

    await userEvent.selectOptions(screen.getByLabelText('Estado'), 'available');

    await waitFor(() => expect(rows()).toHaveLength(15));
    expect(api.listQueries.at(-1)).toMatchObject({ status: 'available', page: 1 });
    expect(currentSearch).toBe('?status=available');
  });

  it('sends the typed text once the user pauses, and keeps it out of the URL', async () => {
    seedCandidates(api, 3);
    renderPage();
    await waitFor(() => expect(rows()).toHaveLength(3));

    await userEvent.type(screen.getByLabelText('Texto'), 'Nombre1');

    await waitFor(() => expect(rows()).toHaveLength(1));
    expect(api.listQueries.at(-1)?.text).toBe('Nombre1');
    expect(currentSearch).toBe('');
  });

  it('renders a URL carrying page, sort and filter as that view', async () => {
    // 60 of these are "new", so a second page of 50 holds 10.
    seedCandidates(api, 120);
    renderPage('/app/candidates?page=2&pageSize=50&sort=lastName&dir=desc&status=new');

    await waitFor(() => expect(rows()).toHaveLength(10));
    expect(api.listQueries).toEqual([
      expect.objectContaining({
        page: 2,
        pageSize: 50,
        sortField: 'lastName',
        sortDirection: 'desc',
        status: 'new',
      }),
    ]);
    expect(screen.getByLabelText('Estado')).toHaveValue('new');
  });

  it('keeps both of two changes made before the page re-renders', async () => {
    seedCandidates(api, 3);
    renderPage();
    await waitFor(() => expect(rows()).toHaveLength(3));

    // Dispatched back to back, with no render between them.
    act(() => {
      fireEvent.change(screen.getByLabelText('Estado'), { target: { value: 'new' } });
      fireEvent.click(screen.getByTestId('candidate-sort-lastName'));
    });

    await waitFor(() => expect(currentSearch).toBe('?status=new&sort=lastName'));
  });

  it('falls back to page 1 for a malformed page number', async () => {
    seedCandidates(api, 3);
    renderPage('/app/candidates?page=abc');

    await waitFor(() => expect(rows()).toHaveLength(3));
    expect(api.listQueries[0].page).toBe(1);
  });

  it('shows the empty result when nothing matches', async () => {
    renderPage();

    expect(await screen.findByTestId('candidate-list-empty')).toBeInTheDocument();
  });

  it('shows an empty page past the end with the true total, not the last page', async () => {
    seedCandidates(api, 3);
    renderPage('/app/candidates?page=9');

    expect(await screen.findByTestId('candidate-list-past-end')).toBeInTheDocument();
    expect(rows()).toHaveLength(0);
    expect(screen.getByTestId('candidate-list-total')).toHaveTextContent('Mostrando 0 de 3');
    expect(screen.getByRole('button', { name: 'Anterior' })).toBeEnabled();
  });

  it('renders the refusal of an unknown sort field instead of ignoring it', async () => {
    seedCandidates(api, 3);
    renderPage('/app/candidates?sort=email');

    expect(await screen.findByTestId('candidate-list-error')).toHaveTextContent(
      'El campo de ordenación no es válido.',
    );
    expect(api.listQueries[0].sortField).toBe('email');
    expect(rows()).toHaveLength(0);
  });

  it.each([
    ['page', async () => userEvent.click(screen.getByRole('button', { name: 'Siguiente' }))],
    ['sort', async () => userEvent.click(screen.getByTestId('candidate-sort-status'))],
    ['filter', async () => userEvent.selectOptions(screen.getByLabelText('CV'), 'no')],
  ])('clears the selection on a %s change', async (_, change) => {
    seedCandidates(api, 30);
    renderPage();
    await waitFor(() => expect(rows()).toHaveLength(25));

    await userEvent.click(selectAll());
    expect(screen.getByTestId('candidate-selection-count')).toHaveTextContent('25 seleccionado(s)');

    await change();

    await waitFor(() =>
      expect(screen.getByTestId('candidate-selection-count')).toHaveTextContent(
        '0 seleccionado(s)',
      ),
    );
    await settle();
    expect(
      within(screen.getByRole('table'))
        .getAllByRole('checkbox')
        .filter((box) => (box as HTMLInputElement).checked),
    ).toHaveLength(0);
  });

  // ---- KTL-31: clickable rows ----

  it('shows the name as the row link, the e-mail as mailto, the phone as text and no «Abrir»', async () => {
    seedCandidates(api, 1, { email: 'ana@example.test', phone: '+34 600 111 222' });
    renderPage();
    await waitFor(() => expect(rows()).toHaveLength(1));
    const row = rows()[0];

    expect(within(row).getByRole('link', { name: 'Nombre0 Apellido000' })).toHaveAttribute(
      'href',
      '/app/candidates/c-000',
    );
    expect(within(row).getByRole('link', { name: 'ana@example.test' })).toHaveAttribute(
      'href',
      'mailto:ana@example.test',
    );
    expect(within(row).getByText('+34 600 111 222').closest('a')).toBeNull();
    expect(within(row).getAllByRole('link')).toHaveLength(2);
    expect(within(row).queryByRole('link', { name: 'Abrir' })).toBeNull();
    expect(document.querySelector('a[href^="tel:"]')).toBeNull();
  });

  it('opens the candidate when a plain part of the row is clicked', async () => {
    seedCandidates(api, 1, { phone: '600111222' });
    renderPage();
    await waitFor(() => expect(rows()).toHaveLength(1));

    await userEvent.click(within(rows()[0]).getByText('600111222'));

    expect(currentPath).toBe('/app/candidates/c-000');
  });

  it('only selects when the selection checkbox or its cell is clicked', async () => {
    seedCandidates(api, 1);
    renderPage();
    await waitFor(() => expect(rows()).toHaveLength(1));
    const checkbox = within(rows()[0]).getByRole('checkbox');

    await userEvent.click(checkbox);
    // A near-miss on the empty space around the checkbox.
    await userEvent.click(checkbox.closest('td')!);

    expect(checkbox).toBeChecked();
    expect(currentPath).toBe('/app/candidates');
  });

  it('keeps the e-mail link from opening the candidate', async () => {
    seedCandidates(api, 1, { email: 'ana@example.test' });
    renderPage();
    await waitFor(() => expect(rows()).toHaveLength(1));
    const email = within(rows()[0]).getByRole('link', { name: 'ana@example.test' });
    // jsdom cannot follow mailto:. The default is stopped at the document, after React's row
    // handler has run, so the row still sees an ordinary link click.
    const stop = (event: Event) => event.preventDefault();
    document.addEventListener('click', stop);

    await userEvent.click(email);

    document.removeEventListener('click', stop);
    expect(currentPath).toBe('/app/candidates');
  });

  it('hides the include-inactive control without the removal permission', async () => {
    granted = ['candidates.read', 'candidates.update'];
    seedCandidates(api, 2);
    renderPage();
    await waitFor(() => expect(rows()).toHaveLength(2));

    expect(screen.queryByRole('checkbox', { name: 'Incluir inactivos' })).toBeNull();
  });

  it('deactivates the selected rows of this page and fetches the view again', async () => {
    seedCandidates(api, 2);
    renderPage();
    await waitFor(() => expect(rows()).toHaveLength(2));

    await userEvent.click(selectAll());
    await userEvent.click(screen.getByRole('button', { name: /Baja lógica masiva/ }));

    await waitFor(() => expect(rows()).toHaveLength(0));
    expect(confirmDialogService.confirm).toHaveBeenCalledWith(
      expect.objectContaining({
        message: 'Se aplicará baja lógica a 2 candidato(s) de esta página.',
      }),
    );
    expect(toastService.show).toHaveBeenCalledWith(
      'Baja lógica aplicada a 2 candidato(s).',
      'success',
    );
    expect([...api.candidates.values()].every((candidate) => !candidate.isActive)).toBe(true);
  });

  it('reactivates removed rows shown through include-inactive', async () => {
    seedCandidates(api, 1, { isActive: false });
    renderPage('/app/candidates?inactive=1');
    await waitFor(() => expect(rows()).toHaveLength(1));
    expect(api.listQueries[0].includeInactive).toBe(true);

    await userEvent.click(selectAll());
    await act(async () => {
      await userEvent.click(screen.getByRole('button', { name: /Alta lógica masiva/ }));
    });

    await waitFor(() =>
      expect(toastService.show).toHaveBeenCalledWith(
        'Alta lógica aplicada a 1 candidato(s).',
        'success',
      ),
    );
    expect(api.candidates.get('c-000')?.isActive).toBe(true);
  });
});
