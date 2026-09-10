import { render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter } from 'react-router';
import { services, type Services } from '../../src/app/core/di/services';
import { ServicesProvider } from '../../src/app/core/di/services-context';
import { EMPTY_CANDIDATE_DRAFT } from '../../src/app/features/candidates/models/candidate.models';
import { CandidateListPage } from '../../src/app/features/candidates/pages/candidate-list-page';
import { CandidateService } from '../../src/app/features/candidates/services/candidate.service';
import { signal } from '../../src/app/core/state/signal';
import { createCandidateTestBed } from './support/candidate-doubles';

describe('CandidateListPage bulk actions', () => {
  let candidateService: CandidateService;
  const toastService = { show: vi.fn() };
  const confirmDialogService = { confirm: vi.fn().mockResolvedValue(true) };
  // usePermission subscribes to the profile signal before reading, so the
  // double has to carry one.
  const authService = {
    profile: signal<unknown>({ id: 'u-1', isActive: true }),
    hasPermission: vi.fn().mockReturnValue(true),
  };

  const renderPage = () =>
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
        <MemoryRouter>
          <CandidateListPage />
        </MemoryRouter>
      </ServicesProvider>,
    );

  /** The select-all box is the first checkbox inside the results table header. */
  const selectAll = () => within(screen.getByRole('table')).getAllByRole('checkbox')[0];

  /** The list screen holds summaries, not aggregates, so assertions read the summary. */
  const summary = (id: string) => candidateService.list(true).find((item) => item.id === id);

  beforeEach(async () => {
    localStorage.clear();
    toastService.show.mockClear();
    confirmDialogService.confirm.mockClear();

    ({ service: candidateService } = createCandidateTestBed());
    await candidateService.ensureLoaded();
    await candidateService.create({
      ...EMPTY_CANDIDATE_DRAFT,
      firstName: 'Ana',
      lastName: 'Rios',
      status: 'available',
      email: 'ana@example.com',
    });
    await candidateService.create({
      ...EMPTY_CANDIDATE_DRAFT,
      firstName: 'Bea',
      lastName: 'Mora',
      status: 'new',
      email: 'bea@example.com',
    });
    const inactive = await candidateService.create({
      ...EMPTY_CANDIDATE_DRAFT,
      firstName: 'Carla',
      lastName: 'Gil',
      status: 'hired',
      email: 'carla@example.com',
    });
    await candidateService.deactivate(inactive.id);
  });

  it('deactivates selected candidates in bulk with confirmation', async () => {
    renderPage();
    const activeIds = candidateService.list().map((item) => item.id);
    expect(activeIds).toHaveLength(2);

    await userEvent.click(selectAll());
    await userEvent.click(screen.getByRole('button', { name: /Baja lógica masiva/ }));

    await waitFor(() => {
      expect(summary(activeIds[0])?.isActive).toBe(false);
    });
    expect(summary(activeIds[1])?.isActive).toBe(false);
    expect(confirmDialogService.confirm).toHaveBeenCalled();
    expect(toastService.show).toHaveBeenCalledWith(
      'Baja lógica aplicada a 2 candidato(s).',
      'success',
    );
  });

  it('reactivates selected candidates in bulk with confirmation', async () => {
    renderPage();
    const inactive = candidateService.list(true).find((item) => !item.isActive)!;

    // Inactive rows are hidden by default, so reveal them before selecting.
    await userEvent.click(screen.getByLabelText(/Incluir inactivos/));
    await userEvent.click(selectAll());
    await userEvent.click(screen.getByRole('button', { name: /Alta lógica masiva/ }));

    await waitFor(() => {
      expect(summary(inactive.id)?.isActive).toBe(true);
    });
    expect(toastService.show).toHaveBeenCalledWith(
      'Alta lógica aplicada a 1 candidato(s).',
      'success',
    );
  });
});
