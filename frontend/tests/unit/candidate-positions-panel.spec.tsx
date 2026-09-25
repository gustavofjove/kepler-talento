import { fireEvent, render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter, Route, Routes } from 'react-router';
import { services, type Services } from '../../src/app/core/di/services';
import { ServicesProvider } from '../../src/app/core/di/services-context';
import { signal } from '../../src/app/core/state/signal';
import { CandidatePositionsPanel } from '../../src/app/features/candidates/components/candidate-positions-panel';
import type { CandidatePosition } from '../../src/app/features/positions/position.models';
import { AppError } from '../../src/app/shared/models/error.models';

const open: CandidatePosition = {
  positionId: 'p-open',
  title: 'Backend',
  positionStatus: 'open',
  stage: 'interview',
  addedAtUtc: '2026-09-10T09:00:00Z',
  updatedAtUtc: '2026-09-10T09:00:00Z',
  version: 5,
};
const closed: CandidatePosition = {
  ...open,
  positionId: 'p-closed',
  title: 'Frontend',
  positionStatus: 'closed',
  stage: 'rejected',
  addedAtUtc: '2026-09-20T09:00:00Z',
};

describe('CandidatePositionsPanel (KTL-30)', () => {
  let granted: string[];
  const positionService = {
    listForCandidate: vi.fn(),
    changeStage: vi.fn(),
    removeCandidate: vi.fn(),
    addCandidate: vi.fn(),
    search: vi.fn(),
  };
  const confirm = vi.fn();
  const toastService = { show: vi.fn() };

  beforeEach(() => {
    vi.clearAllMocks();
    granted = ['candidates.read', 'positions.read', 'positions.manage'];
    positionService.listForCandidate.mockResolvedValue([closed, open]);
    positionService.search.mockResolvedValue({
      items: [
        {
          id: 'p-open',
          title: 'Backend',
          location: '',
          status: 'open',
          updatedAtUtc: '',
          version: 1,
        },
        {
          id: 'p-new',
          title: 'Datos',
          location: 'Madrid',
          status: 'open',
          updatedAtUtc: '',
          version: 1,
        },
      ],
      page: 1,
      pageSize: 10,
      totalCount: 2,
    });
    confirm.mockResolvedValue(true);
  });

  const renderPanel = (candidateIsActive = true) =>
    render(
      <ServicesProvider
        value={
          {
            ...services,
            positionService,
            toastService,
            confirmDialogService: { confirm },
            authService: {
              profile: signal(null),
              hasPermission: (permission: string) => granted.includes(permission),
            },
          } as unknown as Services
        }
      >
        <MemoryRouter initialEntries={['/app/candidates/c-1']}>
          <Routes>
            <Route
              path="/app/candidates/:id"
              element={
                <CandidatePositionsPanel candidateId="c-1" candidateIsActive={candidateIsActive} />
              }
            />
            <Route path="/app/positions/:id" element={<p data-testid="position-page" />} />
          </Routes>
        </MemoryRouter>
      </ServicesProvider>,
    );

  const rows = () => screen.findAllByTestId('candidate-position-row');

  it('lists open positions first and past (closed) ones muted and read-only', async () => {
    renderPanel();

    const [first, second] = await rows();
    expect(within(first).getByRole('link', { name: 'Backend' })).toHaveAttribute(
      'href',
      '/app/positions/p-open',
    );
    expect(within(first).getByRole('combobox', { name: 'Estado en Backend' })).toHaveValue(
      'interview',
    );
    expect(within(second).getByText('Cerrada')).toBeVisible();
    expect(within(second).getByText('Descartado')).toBeVisible();
    expect(second).toHaveClass('position-row-past');
    expect(within(second).queryByRole('combobox')).toBeNull();
    expect(within(second).queryByTestId('remove-from-position')).toBeNull();
    expect(within(second).queryByRole('link', { name: /^Ver/ })).toBeNull();
  });

  it('opens the position when a plain part of the row is clicked, not from its controls', async () => {
    renderPanel();
    positionService.changeStage.mockResolvedValue({ ...open, stage: 'interview', version: 6 });
    const [first] = await rows();

    fireEvent.change(within(first).getByRole('combobox'), { target: { value: 'interview' } });
    await waitFor(() => expect(positionService.changeStage).toHaveBeenCalled());
    expect(screen.queryByTestId('position-page')).toBeNull();

    await userEvent.click(within(first).getByText('Abierta'));
    expect(await screen.findByTestId('position-page')).toBeInTheDocument();
  });

  it('shows the empty state', async () => {
    positionService.listForCandidate.mockResolvedValue([]);
    renderPanel();

    expect(await screen.findByTestId('candidate-positions-empty')).toHaveTextContent(
      'Este candidato no está en ninguna posición.',
    );
  });

  it('offers a reader no controls', async () => {
    granted = ['candidates.read', 'positions.read'];
    renderPanel();

    await rows();
    expect(screen.queryByRole('combobox')).toBeNull();
    expect(screen.queryByTestId('remove-from-position')).toBeNull();
    expect(screen.queryByTestId('candidate-positions-add')).toBeNull();
  });

  it('hides «Añadir a posición» for a removed candidate but keeps restaging', async () => {
    renderPanel(false);

    await rows();
    expect(screen.queryByTestId('candidate-positions-add')).toBeNull();
    expect(screen.getByRole('combobox', { name: 'Estado en Backend' })).toBeEnabled();
  });

  it('restages against the loaded version and reloads after a conflict', async () => {
    positionService.changeStage.mockRejectedValueOnce(
      new AppError('CONFLICT', 'El candidato ha cambiado en esta posición. Vuelva a cargarla.'),
    );
    renderPanel();

    await userEvent.selectOptions(
      await screen.findByRole('combobox', { name: 'Estado en Backend' }),
      'hired',
    );

    expect(positionService.changeStage).toHaveBeenCalledWith('p-open', 'c-1', 'hired', 5);
    await waitFor(() => expect(positionService.listForCandidate).toHaveBeenCalledTimes(2));
    expect(toastService.show).toHaveBeenCalledWith(
      'El candidato ha cambiado en esta posición. Vuelva a cargarla.',
      'error',
    );
  });

  it('removes only after confirmation', async () => {
    confirm.mockResolvedValueOnce(false);
    renderPanel();
    const remove = await screen.findByRole('button', { name: 'Quitar de la posición Backend' });

    await userEvent.click(remove);
    expect(positionService.removeCandidate).not.toHaveBeenCalled();

    await userEvent.click(remove);
    await waitFor(() =>
      expect(positionService.removeCandidate).toHaveBeenCalledWith('p-open', 'c-1'),
    );
    await waitFor(() => expect(screen.getAllByTestId('candidate-position-row')).toHaveLength(1));
  });

  it('adds the candidate to an open position through the picker', async () => {
    positionService.addCandidate.mockResolvedValue({});
    renderPanel();
    await rows();

    await userEvent.click(screen.getByTestId('candidate-positions-add'));
    const dialog = within(await screen.findByRole('dialog'));
    const options = await dialog.findAllByTestId('position-picker-option');
    expect(positionService.search).toHaveBeenCalledWith(
      expect.objectContaining({ status: 'open', text: '' }),
      expect.any(AbortSignal),
    );
    expect(options[0]).toHaveAttribute('aria-disabled', 'true');

    await userEvent.click(options[0]);
    expect(positionService.addCandidate).not.toHaveBeenCalled();
    await userEvent.click(options[1]);

    await waitFor(() => expect(screen.queryByRole('dialog')).toBeNull());
    expect(positionService.addCandidate).toHaveBeenCalledWith('p-new', 'c-1');
    expect(positionService.listForCandidate).toHaveBeenCalledTimes(2);
  });
});
