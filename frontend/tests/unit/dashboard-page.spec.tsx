import { render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter, Route, Routes, useLocation } from 'react-router';
import { services, type Services } from '../../src/app/core/di/services';
import { ServicesProvider } from '../../src/app/core/di/services-context';
import { signal } from '../../src/app/core/state/signal';
import { localDay } from '../../src/app/core/i18n/format';
import type {
  CandidateListItem,
  CandidateListPage,
  CandidateListQuery,
} from '../../src/app/features/candidates/models/candidate.models';
import { DashboardPage } from '../../src/app/features/dashboard/dashboard-page';
import type {
  PositionListItem,
  PositionPage,
} from '../../src/app/features/positions/position.models';
import {
  EMPTY_SEARCH_FILTERS,
  type SearchPreset,
} from '../../src/app/features/search/models/search.models';
import type { PresetsState } from '../../src/app/features/search/services/search-presets.service';
import type { Permission } from '../../src/app/shared/models/auth.models';

const READER: Permission[] = ['candidates.read', 'positions.read'];

const daysAgo = (days: number) => {
  const day = new Date();
  day.setDate(day.getDate() - days);
  return localDay(day);
};

const candidate = (id: string, overrides: Partial<CandidateListItem> = {}): CandidateListItem => ({
  candidateId: id,
  firstName: 'Nombre',
  lastName: id,
  phone: '+34 600 000 001',
  email: `${id}@ejemplo.test`,
  availabilityState: 'unknown',
  availabilityCheckedOn: null,
  hasPrimaryCv: true,
  primaryCvPreviewable: false,
  primaryCvDownloadable: false,
  updatedAt: '2026-10-01T09:00:00Z',
  isActive: true,
  ...overrides,
});

const page = (items: CandidateListItem[], totalCount = items.length): CandidateListPage => ({
  items,
  page: 1,
  pageSize: 5,
  totalCount,
});

const position = (
  id: string,
  stageCounts: PositionListItem['stageCounts'] = {
    new: 0,
    shortlisted: 0,
    interview: 0,
    hired: 0,
    rejected: 0,
  },
): PositionListItem => ({
  id,
  title: `Posición ${id}`,
  location: 'Madrid',
  status: 'open',
  updatedAtUtc: '2026-10-01T09:00:00Z',
  version: 1,
  candidateCount: Object.values(stageCounts).reduce((sum, value) => sum + value, 0),
  stageCounts,
});

const preset = (name: string, lastUsedAt?: string): SearchPreset => ({
  id: `0199a1b2-c3d4-7e5f-8a9b-${name
    .padEnd(12, '0')
    .slice(0, 12)
    .replace(/[^0-9a-f]/gi, 'a')}`,
  name,
  filters: structuredClone(EMPTY_SEARCH_FILTERS),
  createdAt: '2026-01-01T00:00:00Z',
  updatedAt: '2026-01-01T00:00:00Z',
  lastUsedAt,
  version: 1,
});

/** What the candidate search answers, by the shape of each dashboard query. */
interface CandidateData {
  recentAdded: CandidateListPage;
  recentAvailable: CandidateListPage;
  unavailable: number;
  withoutCv: number;
  everyone: number;
}

function LocationProbe() {
  const location = useLocation();
  return <p data-testid="location">{`${location.pathname}${location.search}`}</p>;
}

describe('DashboardPage «Inicio» (KTL-40)', () => {
  let granted: Permission[];
  let data: CandidateData;
  let listPage: ReturnType<typeof vi.fn>;
  let positionSearch: ReturnType<typeof vi.fn>;
  let presetsState: ReturnType<typeof signal<PresetsState>>;
  let presetsLoad: ReturnType<typeof vi.fn>;

  beforeEach(() => {
    granted = [...READER];
    data = {
      recentAdded: page(
        [
          candidate('nuevo-1', { hasPrimaryCv: false }),
          candidate('nuevo-2', {
            availabilityState: 'available',
            availabilityCheckedOn: daysAgo(1),
          }),
        ],
        10,
      ),
      recentAvailable: page(
        [
          candidate('disp-1', {
            availabilityState: 'available',
            availabilityCheckedOn: daysAgo(2),
          }),
          candidate('disp-2', {
            availabilityState: 'available',
            availabilityCheckedOn: daysAgo(4),
          }),
          candidate('disp-3', {
            availabilityState: 'available',
            availabilityCheckedOn: daysAgo(6),
          }),
        ],
        4,
      ),
      unavailable: 2,
      withoutCv: 23,
      everyone: 13,
    };
    listPage = vi.fn((query: CandidateListQuery) => {
      if (query.includeInactive) return Promise.resolve(page([], data.everyone));
      if (query.availability === 'available') return Promise.resolve(data.recentAvailable);
      if (query.availability === 'unavailable') return Promise.resolve(page([], data.unavailable));
      if (query.hasCv === 'no') return Promise.resolve(page([], data.withoutCv));
      if (query.sortField === 'createdAt') return Promise.resolve(data.recentAdded);
      return Promise.reject(new Error(`unexpected query ${JSON.stringify(query)}`));
    });
    const positions: PositionPage = {
      items: [
        position('a', { new: 3, shortlisted: 0, interview: 1, hired: 0, rejected: 2 }),
        position('b'),
        position('c'),
        position('d'),
        position('e'),
      ],
      page: 1,
      pageSize: 5,
      totalCount: 6,
    };
    positionSearch = vi.fn().mockResolvedValue(positions);
    presetsState = signal<PresetsState>({ status: 'idle', presets: [] });
    presetsLoad = vi.fn(() => {
      presetsState.set({
        status: 'loaded',
        presets: [
          preset('Nunca'),
          preset('Hace tres dias', new Date(Date.now() - 3 * 86_400_000).toISOString()),
          preset('Ayer', new Date(Date.now() - 86_400_000).toISOString()),
        ],
      });
      return Promise.resolve([]);
    });
  });

  const renderPage = () => {
    const profile = signal<unknown>({ id: 'u-1', isActive: true });
    return render(
      <ServicesProvider
        value={
          {
            ...services,
            candidateService: { listPage },
            positionService: { search: positionSearch, list: vi.fn() },
            searchPresetsService: { state: presetsState, load: presetsLoad },
            authService: {
              profile,
              hasPermission: (permission: Permission) => granted.includes(permission),
            },
          } as unknown as Services
        }
      >
        <MemoryRouter initialEntries={['/app']}>
          <Routes>
            <Route path="/app" element={<DashboardPage />} />
            <Route path="*" element={<LocationProbe />} />
          </Routes>
        </MemoryRouter>
      </ServicesProvider>,
    );
  };

  const region = (name: string | RegExp) => screen.getByRole('region', { name });

  it('is titled Inicio', () => {
    renderPage();

    expect(screen.getByRole('heading', { level: 1, name: 'Inicio' })).toBeInTheDocument();
    expect(
      screen.getByText('Resumen de candidatos, posiciones y búsquedas guardadas.'),
    ).toBeInTheDocument();
  });

  it('shows the active total and its availability split, each figure linking to its list', async () => {
    renderPage();

    const card = region('Candidatos');
    expect(await within(card).findByTestId('kpi-active')).toHaveTextContent('10 activos');
    expect(within(card).getByTestId('kpi-active')).toHaveAttribute('href', '/app/candidates');
    expect(within(card).getByTestId('kpi-available')).toHaveTextContent('4 disponibles');
    expect(within(card).getByTestId('kpi-available')).toHaveAttribute(
      'href',
      '/app/candidates?availability=available&sort=availabilityCheckedOn',
    );
    expect(within(card).getByTestId('kpi-unavailable')).toHaveTextContent('2 no disponibles');
    expect(within(card).getByTestId('kpi-unavailable')).toHaveAttribute(
      'href',
      '/app/candidates?availability=unavailable',
    );
    expect(within(card).getByTestId('kpi-unknown')).toHaveTextContent('4 sin comprobar');
    expect(within(card).getByTestId('kpi-unknown')).toHaveAttribute(
      'href',
      '/app/candidates?availability=unknown',
    );
    expect(within(card).getByTestId('availability-bar')).toHaveAttribute('aria-hidden', 'true');
  });

  it('uses the singular for one candidate and hides the bar for none', async () => {
    data.recentAdded = page([candidate('solo')], 1);
    data.recentAvailable = page([], 0);
    data.unavailable = 1;
    const first = renderPage();
    expect(await screen.findByTestId('kpi-active')).toHaveTextContent('1 activo');
    expect(screen.getByTestId('kpi-unavailable')).toHaveTextContent('1 no disponible');
    first.unmount();

    data.recentAdded = page([], 0);
    data.unavailable = 0;
    renderPage();
    expect(await screen.findByTestId('kpi-active')).toHaveTextContent('0 activos');
    expect(screen.queryByTestId('availability-bar')).not.toBeInTheDocument();
  });

  it('shows removed candidates as text to candidates.delete holders only', async () => {
    granted = [...READER, 'candidates.delete'];
    const withDelete = renderPage();

    const inactive = await screen.findByTestId('kpi-inactive');
    expect(inactive).toHaveTextContent('3 inactivos');
    expect(inactive.closest('a')).toBeNull();
    expect(listPage).toHaveBeenCalledWith(
      expect.objectContaining({ includeInactive: true }),
      expect.any(AbortSignal),
    );
    withDelete.unmount();

    listPage.mockClear();
    granted = [...READER];
    renderPage();
    await screen.findByTestId('kpi-active');
    expect(screen.queryByTestId('kpi-inactive')).not.toBeInTheDocument();
    expect(listPage).not.toHaveBeenCalledWith(
      expect.objectContaining({ includeInactive: true }),
      expect.anything(),
    );
  });

  it('links the «Sin CV principal» tile to the list of candidates without one', async () => {
    renderPage();

    const tile = await screen.findByTestId('kpi-without-cv');
    expect(tile).toHaveTextContent('23');
    expect(tile).toHaveAttribute('href', '/app/candidates?cv=no');
    expect(tile).toHaveAccessibleName('Sin CV principal: 23');
  });

  it('lists the open positions with a count per stage and opens one from its row', async () => {
    renderPage();

    expect(await screen.findByTestId('kpi-open-positions')).toHaveTextContent('6');
    expect(positionSearch).toHaveBeenCalledWith(
      { status: 'open', sortField: 'updatedAt', sortDirection: 'desc', page: 1, pageSize: 5 },
      expect.any(AbortSignal),
    );
    const panel = region('Posiciones abiertas');
    const rows = within(panel).getAllByTestId('dashboard-position-row');
    expect(rows).toHaveLength(5);
    const counts = ['new', 'shortlisted', 'interview', 'hired', 'rejected', 'total'].map(
      (stage) => within(rows[0]).getByTestId(`stage-count-${stage}`).textContent,
    );
    expect(counts).toEqual(['3', '0', '1', '0', '2', '6']);
    expect(within(panel).getByRole('columnheader', { name: 'Preseleccionado' })).toBeVisible();
    expect(within(panel).getByTestId('dashboard-positions-all')).toHaveTextContent('Ver todas (6)');
    expect(within(rows[0]).getByRole('link', { name: 'Posición a' })).toHaveAttribute(
      'href',
      '/app/positions/a',
    );

    await userEvent.click(within(rows[1]).getByTestId('stage-count-new'));
    expect(screen.getByTestId('location')).toHaveTextContent('/app/positions/b');
  });

  it('lists the candidates most recently confirmed available, with the elapsed time', async () => {
    renderPage();

    const panel = region('Últimos disponibles');
    const rows = await within(panel).findAllByTestId('dashboard-recent-available-row');
    expect(rows.map((row) => within(row).getByRole('link').textContent)).toEqual([
      'Nombre disp-1',
      'Nombre disp-2',
      'Nombre disp-3',
    ]);
    expect(within(rows[0]).getByRole('link')).toHaveAttribute('href', '/app/candidates/disp-1');
    expect(
      within(rows[1]).getByTestId('dashboard-recent-available-availability-elapsed'),
    ).toHaveTextContent('hace 4 días');
    expect(within(panel).getByTestId('dashboard-recent-available-all')).toHaveTextContent(
      'Ver todos (4)',
    );
    expect(within(panel).getByTestId('dashboard-recent-available-all')).toHaveAttribute(
      'href',
      '/app/candidates?availability=available&sort=availabilityCheckedOn',
    );
  });

  it('lists the newest candidates with their availability and flags a missing CV', async () => {
    renderPage();

    const panel = region('Últimos añadidos');
    const rows = await within(panel).findAllByTestId('dashboard-recent-added-row');
    expect(rows).toHaveLength(2);
    expect(within(rows[0]).getByTestId('dashboard-recent-added-no-cv')).toHaveTextContent('Sin CV');
    expect(within(rows[1]).queryByTestId('dashboard-recent-added-no-cv')).toBeNull();
    expect(
      within(rows[1]).getByTestId('dashboard-recent-added-availability-chip'),
    ).toHaveTextContent('Disponible');
    expect(within(panel).getByTestId('dashboard-recent-added-all')).toHaveAttribute(
      'href',
      '/app/candidates?sort=createdAt',
    );
  });

  it('shows names only: no e-mail, phone or CV action in the candidate panels', async () => {
    renderPage();

    const panels = [region('Últimos disponibles'), region('Últimos añadidos')];
    await within(panels[0]).findAllByTestId('dashboard-recent-available-row');
    for (const panel of panels) {
      expect(panel.textContent).not.toMatch(/@ejemplo\.test|\+34/);
      expect(within(panel).queryByRole('button')).toBeNull();
    }
  });

  it('lists saved searches by last use, never-used last, each opening the search with it', async () => {
    granted = [...READER, 'presets.manage'];
    renderPage();

    const panel = region('Búsquedas guardadas');
    const rows = await within(panel).findAllByTestId('dashboard-preset-row');
    expect(rows.map((row) => within(row).getByRole('link').textContent)).toEqual([
      'Ayer',
      'Hace tres dias',
      'Nunca',
    ]);
    expect(rows[0]).toHaveTextContent('Usada ayer');
    expect(rows[1]).toHaveTextContent('Usada hace 3 días');
    expect(rows[2]).toHaveTextContent('Sin usar');
    expect(within(rows[0]).getByRole('link')).toHaveAttribute(
      'href',
      `/app/search?preset=${preset('Ayer').id}`,
    );
    expect(within(panel).getByTestId('dashboard-manage-presets')).toHaveAttribute(
      'href',
      '/app/admin/presets',
    );
  });

  it('offers preset management only to presets.manage holders', async () => {
    renderPage();

    await within(region('Búsquedas guardadas')).findAllByTestId('dashboard-preset-row');
    expect(screen.queryByTestId('dashboard-manage-presets')).not.toBeInTheDocument();
  });

  describe('permissions per panel', () => {
    it('shows a position reader only the positions, and sends no candidate or preset request', async () => {
      granted = ['positions.read'];
      renderPage();

      await screen.findAllByTestId('dashboard-position-row');
      expect(screen.queryByTestId('dashboard-candidates')).not.toBeInTheDocument();
      expect(screen.queryByTestId('dashboard-recent-available')).not.toBeInTheDocument();
      expect(screen.queryByTestId('dashboard-saved-searches')).not.toBeInTheDocument();
      expect(screen.queryByTestId('kpi-without-cv')).not.toBeInTheDocument();
      expect(listPage).not.toHaveBeenCalled();
      expect(presetsLoad).not.toHaveBeenCalled();
    });

    it('shows a candidate reader only the candidate panels, and sends no position request', async () => {
      granted = ['candidates.read'];
      renderPage();

      await screen.findByTestId('kpi-active');
      expect(screen.queryByTestId('dashboard-positions')).not.toBeInTheDocument();
      expect(screen.queryByTestId('kpi-open-positions-tile')).not.toBeInTheDocument();
      expect(positionSearch).not.toHaveBeenCalled();
      expect(listPage).toHaveBeenCalledTimes(4);
      expect(presetsLoad).toHaveBeenCalledTimes(1);
    });

    it('tells an actor with neither read permission so and sends no request', () => {
      granted = [];
      renderPage();

      expect(screen.getByTestId('dashboard-no-access')).toHaveTextContent(
        'No tienes acceso a ningún resumen.',
      );
      expect(listPage).not.toHaveBeenCalled();
      expect(positionSearch).not.toHaveBeenCalled();
      expect(presetsLoad).not.toHaveBeenCalled();
    });

    it('stays within seven requests for an actor holding everything', async () => {
      granted = [...READER, 'candidates.delete', 'presets.manage'];
      renderPage();

      await screen.findByTestId('kpi-inactive');
      expect(listPage).toHaveBeenCalledTimes(5);
      expect(positionSearch).toHaveBeenCalledTimes(1);
      expect(presetsLoad).toHaveBeenCalledTimes(1);
      for (const [query] of listPage.mock.calls as [CandidateListQuery][]) {
        expect(query.text).toBe('');
      }
    });
  });

  describe('header actions', () => {
    it('shows each create action only with its permission', () => {
      granted = [...READER, 'candidates.create', 'positions.manage', 'candidates.import'];
      const all = renderPage();
      const actions = screen.getByTestId('dashboard-actions');
      expect(within(actions).getByRole('link', { name: 'Alta de candidato' })).toHaveAttribute(
        'href',
        '/app/candidates/new',
      );
      expect(within(actions).getByRole('link', { name: 'Nueva posición' })).toHaveAttribute(
        'href',
        '/app/positions/new',
      );
      expect(within(actions).getByRole('link', { name: 'Importar candidatos' })).toHaveAttribute(
        'href',
        '/app/admin/import',
      );
      all.unmount();

      granted = [...READER, 'positions.manage'];
      renderPage();
      expect(screen.queryByRole('link', { name: 'Alta de candidato' })).not.toBeInTheDocument();
      expect(screen.queryByRole('link', { name: 'Importar candidatos' })).not.toBeInTheDocument();
    });

    it('no longer renders the operations block or the primary-CV figure', () => {
      renderPage();

      expect(screen.queryByText('Centro operativo')).not.toBeInTheDocument();
      expect(screen.queryByText('Con CV principal')).not.toBeInTheDocument();
      expect(screen.queryByTestId('kpi-with-primary-cv')).not.toBeInTheDocument();
      expect(screen.queryByTestId('dashboard-error')).not.toBeInTheDocument();
    });
  });

  it('announces loading in each panel', () => {
    listPage.mockReturnValue(new Promise(() => undefined));
    positionSearch.mockReturnValue(new Promise(() => undefined));
    renderPage();

    expect(within(region('Posiciones abiertas')).getByRole('status')).toHaveTextContent(
      'Cargando…',
    );
    expect(within(region('Últimos añadidos')).getByRole('status')).toBeInTheDocument();
  });

  it('keeps the candidate panels when the positions request fails', async () => {
    positionSearch.mockRejectedValue(new Error('caída'));
    renderPage();

    expect(await screen.findByTestId('dashboard-positions-error')).toHaveTextContent(
      'No se ha podido cargar este resumen.',
    );
    expect(screen.getByTestId('kpi-open-positions-tile-error')).toBeInTheDocument();
    expect(await screen.findByTestId('kpi-active')).toHaveTextContent('10 activos');
    expect(await screen.findAllByTestId('dashboard-recent-added-row')).toHaveLength(2);
  });

  it('shows each panel its own empty state', async () => {
    granted = [...READER, 'positions.manage'];
    data.recentAdded = page([], 0);
    data.recentAvailable = page([], 0);
    data.unavailable = 0;
    data.withoutCv = 0;
    positionSearch.mockResolvedValue({ items: [], page: 1, pageSize: 5, totalCount: 0 });
    presetsLoad.mockImplementation(() => {
      presetsState.set({ status: 'loaded', presets: [] });
      return Promise.resolve([]);
    });
    renderPage();

    expect(await screen.findByTestId('dashboard-positions-empty')).toHaveTextContent(
      'No hay posiciones abiertas.',
    );
    expect(
      within(region('Posiciones abiertas')).getByRole('link', { name: 'Nueva posición' }),
    ).toHaveAttribute('href', '/app/positions/new');
    expect(await screen.findByTestId('dashboard-recent-available-empty')).toHaveTextContent(
      'Ningún candidato está marcado como disponible.',
    );
    expect(screen.getByTestId('dashboard-recent-added-empty')).toHaveTextContent(
      'Todavía no hay candidatos.',
    );
    expect(await screen.findByTestId('dashboard-saved-searches-empty')).toHaveTextContent(
      'No hay búsquedas guardadas.',
    );
  });

  it('shows a preset failure in its panel only', async () => {
    presetsLoad.mockImplementation(() => {
      presetsState.set({ status: 'failed', presets: [] });
      return Promise.reject(new Error('caída'));
    });
    renderPage();

    expect(await screen.findByTestId('dashboard-saved-searches-error')).toBeInTheDocument();
    await waitFor(() => expect(screen.getByTestId('kpi-active')).toBeInTheDocument());
  });
});
