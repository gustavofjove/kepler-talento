import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { services, type Services } from '../../src/app/core/di/services';
import { ServicesProvider } from '../../src/app/core/di/services-context';
import { localDay } from '../../src/app/core/i18n/format';
import { signal } from '../../src/app/core/state/signal';
import { CandidateAvailabilityBlock } from '../../src/app/features/candidates/components/candidate-availability';
import { useCandidate } from '../../src/app/features/candidates/use-candidates';
import { createCandidateTestBed, type CandidateTestBed } from './support/candidate-doubles';

describe('CandidateAvailabilityBlock', () => {
  let bed: CandidateTestBed;
  let permissions: Set<string>;
  const toastService = { show: vi.fn() };

  beforeEach(async () => {
    vi.clearAllMocks();
    bed = createCandidateTestBed();
    bed.api.seed({ id: 'candidate-1', firstName: 'Ana' });
    await bed.service.ensureAggregate('candidate-1');
    permissions = new Set(['candidates.update']);
  });

  function Block() {
    const candidates = useCandidate('candidate-1');
    return <CandidateAvailabilityBlock candidate={candidates.find('candidate-1')!} />;
  }

  function renderBlock() {
    return render(
      <ServicesProvider
        value={
          {
            ...services,
            candidateService: bed.service,
            toastService,
            authService: {
              profile: signal(null),
              hasPermission: (permission: string) => permissions.has(permission),
            },
          } as unknown as Services
        }
      >
        <Block />
      </ServicesProvider>,
    );
  }

  it('shows an unchecked candidate and records a dated check, then undoes it', async () => {
    const user = userEvent.setup();
    renderBlock();
    expect(screen.getByTestId('candidate-availability-line')).toHaveTextContent(/^Sin comprobar$/);
    expect(screen.queryByTestId('availability-reconfirm')).toBeNull();

    await user.click(screen.getByTestId('availability-change'));
    expect(screen.getByTestId('availability-save')).toHaveAccessibleName('Registrar');
    expect(screen.queryByTestId('availability-checked-on')).toBeNull();
    await user.click(screen.getByTestId('availability-state-available'));
    expect(screen.getByTestId('availability-checked-on')).toHaveValue(localDay());
    await user.click(screen.getByTestId('availability-save'));

    await waitFor(() =>
      expect(bed.service.find('candidate-1')?.availability.state).toBe('available'),
    );
    expect(screen.getByTestId('candidate-availability-status')).toHaveTextContent(
      'Comprobación registrada',
    );
    expect(screen.getByTestId('candidate-availability-meta')).toHaveTextContent('Test User');
    await user.click(screen.getByTestId('availability-undo'));
    await waitFor(() =>
      expect(bed.service.find('candidate-1')?.availability.state).toBe('unknown'),
    );
    expect(screen.getByTestId('candidate-availability-status')).toHaveTextContent('restaurada');
  });

  it('reconfirms a known check and returns focus after cancelling the inline form', async () => {
    const user = userEvent.setup();
    bed = createCandidateTestBed();
    bed.api.seed({
      id: 'candidate-1',
      availability: {
        state: 'available',
        checkedOn: '2026-09-01',
        until: '',
        checkedByDisplayName: null,
      },
    });
    await bed.service.ensureAggregate('candidate-1');
    renderBlock();
    await user.click(screen.getByTestId('availability-reconfirm'));
    await waitFor(() =>
      expect(bed.service.find('candidate-1')?.availability.checkedOn).toBe(localDay()),
    );
    await user.click(screen.getByTestId('availability-change'));
    await user.click(screen.getByTestId('availability-cancel'));
    await waitFor(() => expect(screen.getByTestId('availability-change')).toHaveFocus());
  });

  it('is read-only without update permission', () => {
    permissions.clear();
    renderBlock();
    expect(screen.queryByTestId('availability-change')).toBeNull();
  });

  it('is read-only for a removed candidate', async () => {
    bed = createCandidateTestBed();
    bed.api.seed({ id: 'candidate-1', firstName: 'Ana', isActive: false });
    await bed.service.ensureAggregate('candidate-1');
    renderBlock();
    expect(screen.queryByTestId('availability-change')).toBeNull();
  });

  it('marks an expired until date and hides reconfirmation', async () => {
    bed = createCandidateTestBed();
    bed.api.seed({
      id: 'candidate-1',
      availability: {
        state: 'unavailable',
        checkedOn: '2026-03-01',
        until: '2026-03-02',
        checkedByDisplayName: 'Ana',
      },
    });
    await bed.service.ensureAggregate('candidate-1');
    renderBlock();
    expect(screen.getByTestId('candidate-availability-line')).toHaveTextContent('vencido');
    expect(screen.getByTestId('candidate-availability-lapsed-hint')).toBeVisible();
    expect(screen.queryByTestId('availability-reconfirm')).toBeNull();
  });
});
