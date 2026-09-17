import { readFileSync } from 'node:fs';
import { join } from 'node:path';
import { render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter, Route, Routes } from 'react-router';
import { services, type Services } from '../../src/app/core/di/services';
import { ServicesProvider } from '../../src/app/core/di/services-context';
import { RequirePermission } from '../../src/app/core/routing/require-permission';
import { signal } from '../../src/app/core/state/signal';
import { AuditPage } from '../../src/app/features/admin/audit/audit-page';
import {
  EMPTY_AUDIT_FILTERS,
  type AuditFilters,
  type AuditPage as AuditPageData,
} from '../../src/app/features/admin/audit/audit.logic';
import type { AuditState } from '../../src/app/features/admin/audit/audit.service';
import type { Permission } from '../../src/app/shared/models/auth.models';
import es from '../../src/assets/i18n/es.json';
import { auditEvent, auditPage } from './support/audit-doubles';

const copy = es as Record<string, string>;

function auditServiceDouble(pages: AuditPageData[], names: Record<string, string> = {}) {
  const state = signal<AuditState>({ filters: EMPTY_AUDIT_FILTERS, page: auditPage() });
  let call = 0;
  return {
    state,
    load: vi.fn(async (filters: AuditFilters) => {
      const page = pages[Math.min(call, pages.length - 1)];
      call += 1;
      state.set({ filters, page });
      return page;
    }),
    resolveActorNames: vi.fn().mockResolvedValue(names),
  };
}

function renderPage(
  auditService: ReturnType<typeof auditServiceDouble>,
  permissions: Permission[] = ['audit.read', 'users.manage'],
  initialEntry = '/app/admin/audit',
) {
  const profile = { id: 'u-0', permissions };
  const authService = {
    profile: () => profile,
    hasPermission: (permission: Permission) => permissions.includes(permission),
  };
  Object.assign(authService.profile, { subscribe: () => () => undefined });
  const toastService = { show: vi.fn() };
  render(
    <ServicesProvider
      value={
        {
          ...services,
          auditService: auditService as never,
          authService: authService as never,
          toastService: toastService as never,
        } as Services
      }
    >
      <MemoryRouter initialEntries={[initialEntry]}>
        <Routes>
          <Route path="/app" element={<p data-testid="app-home" />} />
          <Route element={<RequirePermission permission="audit.read" />}>
            <Route path="/app/admin/audit" element={<AuditPage />} />
          </Route>
        </Routes>
      </MemoryRouter>
    </ServicesProvider>,
  );
  return { toastService };
}

const threeActors = auditPage({
  totalCount: 3,
  items: [
    auditEvent({ id: 'e-user', actorKind: 'user', actorUserId: 'u-1' }),
    auditEvent({
      id: 'e-system',
      eventType: 'document.scan',
      actorKind: 'system',
      actorUserId: null,
    }),
    auditEvent({
      id: 'e-unknown',
      eventType: 'candidate.updated',
      actorKind: 'unknown',
      actorUserId: null,
      outcomeCode: null,
    }),
  ],
});

describe('AuditPage', () => {
  it('lists each event with its type, subject, actor, outcome and timestamp', async () => {
    const service = auditServiceDouble([threeActors], { 'u-1': 'Ana Pérez' });
    renderPage(service);

    const row = await screen.findByTestId('audit-row-e-user');
    expect(row).toHaveTextContent(copy['admin.audit.eventType.candidate.read']);
    expect(row).toHaveTextContent('0192aaaa0000700080000000000000c1');
    expect(row).toHaveTextContent('served');
    expect(within(row).getAllByRole('cell')[0].textContent).not.toBe('');
    expect(screen.getByTestId('audit-total')).toHaveTextContent('3');
    expect(service.load).toHaveBeenCalledWith(EMPTY_AUDIT_FILTERS, 1);
  });

  it('resolves user actor names once per page through the users endpoint', async () => {
    const service = auditServiceDouble([threeActors], { 'u-1': 'Ana Pérez' });
    renderPage(service);

    await waitFor(() =>
      expect(
        within(screen.getByTestId('audit-row-e-user')).getByTestId('audit-actor'),
      ).toHaveTextContent('Ana Pérez'),
    );
    expect(service.resolveActorNames).toHaveBeenCalledOnce();
    expect(service.resolveActorNames).toHaveBeenCalledWith(['u-1']);
  });

  it('shows the internal id, without asking, to a reader who may not list users', async () => {
    const service = auditServiceDouble([threeActors], { 'u-1': 'Ana Pérez' });
    renderPage(service, ['audit.read']);

    const actor = within(await screen.findByTestId('audit-row-e-user')).getByTestId('audit-actor');
    expect(actor).toHaveTextContent('u-1');
    expect(service.resolveActorNames).not.toHaveBeenCalled();
  });

  it('labels the system and an unknown actor distinctly, never as an empty cell', async () => {
    renderPage(auditServiceDouble([threeActors]));

    const system = within(await screen.findByTestId('audit-row-e-system')).getByTestId(
      'audit-actor',
    );
    const unknown = within(screen.getByTestId('audit-row-e-unknown')).getByTestId('audit-actor');
    expect(system).toHaveTextContent(copy['admin.audit.actor.system']);
    expect(unknown).toHaveTextContent(copy['admin.audit.actor.unknown']);
    expect(system.textContent).not.toBe(unknown.textContent);
    expect(screen.getByTestId('audit-row-e-unknown')).toHaveTextContent(
      copy['admin.audit.noOutcome'],
    );
  });

  it('applies the filters the reader chose and starts again from the first page', async () => {
    const user = userEvent.setup();
    const service = auditServiceDouble([threeActors]);
    renderPage(service);
    await screen.findByTestId('audit-table');

    await user.selectOptions(screen.getByTestId('audit-filter-event-type'), 'candidate.read');
    await user.type(screen.getByTestId('audit-filter-actor'), 'system');
    await user.type(screen.getByTestId('audit-filter-subject'), 'abc');
    await user.click(screen.getByTestId('audit-apply'));

    await waitFor(() =>
      expect(service.load).toHaveBeenLastCalledWith(
        { ...EMPTY_AUDIT_FILTERS, eventType: 'candidate.read', actor: 'system', subject: 'abc' },
        1,
      ),
    );
  });

  it('pages forward and back with the applied filters', async () => {
    const user = userEvent.setup();
    const first = auditPage({ totalCount: 60, page: 1, items: [auditEvent({ id: 'p1' })] });
    const second = auditPage({ totalCount: 60, page: 2, items: [auditEvent({ id: 'p2' })] });
    const service = auditServiceDouble([first, second, first]);
    renderPage(service);

    await screen.findByTestId('audit-row-p1');
    expect(screen.getByTestId('audit-previous')).toBeDisabled();
    await user.click(screen.getByTestId('audit-next'));
    expect(await screen.findByTestId('audit-row-p2')).toBeInTheDocument();
    expect(service.load).toHaveBeenLastCalledWith(EMPTY_AUDIT_FILTERS, 2);

    await user.click(screen.getByTestId('audit-previous'));
    await waitFor(() => expect(service.load).toHaveBeenLastCalledWith(EMPTY_AUDIT_FILTERS, 1));
  });

  it('reports a failed load with a toast and an empty trail', async () => {
    const service = auditServiceDouble([auditPage()]);
    service.load.mockRejectedValueOnce(new Error(''));
    const { toastService } = renderPage(service);

    await waitFor(() =>
      expect(toastService.show).toHaveBeenCalledWith(copy['admin.audit.errors.load'], 'error'),
    );
    expect(screen.getByTestId('audit-empty')).toBeInTheDocument();
  });

  it('keeps a name on every filter control the end-to-end suite binds to', async () => {
    renderPage(auditServiceDouble([auditPage()]));
    await screen.findByTestId('audit-filters');

    expect(screen.getByTestId('audit-filter-from')).toHaveAttribute('name', 'from');
    expect(screen.getByTestId('audit-filter-to')).toHaveAttribute('name', 'to');
    expect(screen.getByTestId('audit-filter-event-type')).toHaveAttribute('name', 'eventType');
    expect(screen.getByTestId('audit-filter-actor')).toHaveAttribute('name', 'actor');
    expect(screen.getByTestId('audit-filter-subject')).toHaveAttribute('name', 'subject');
  });
});

describe('Auditoría route', () => {
  it('redirects a reader without audit.read and never loads the trail', () => {
    const service = auditServiceDouble([threeActors]);
    renderPage(service, ['users.manage', 'roles.manage', 'candidates.read']);

    expect(screen.getByTestId('app-home')).toBeInTheDocument();
    expect(screen.queryByTestId('audit-table')).not.toBeInTheDocument();
    expect(service.load).not.toHaveBeenCalled();
  });

  it('is declared in the application routes behind the audit.read guard', () => {
    const source = readFileSync(join(process.cwd(), 'src/app/app.tsx'), 'utf8');

    expect(source).toMatch(
      /<RequirePermission permission="audit\.read" \/>,\s*children: \[\{ path: 'admin\/audit', element: <AuditPage \/> \}\]/,
    );
  });
});
