import {
  CANDIDATE_QUERIES,
  DASHBOARD_HREFS,
  DASHBOARD_ROWS,
  OPEN_POSITIONS_QUERY,
  availabilitySplit,
  barSegments,
  candidateHref,
  inactiveCount,
  positionHref,
  topPresets,
} from '../../src/app/features/dashboard/dashboard.logic';
import {
  EMPTY_SEARCH_FILTERS,
  type SearchPreset,
} from '../../src/app/features/search/models/search.models';
import {
  isPresetId,
  presetSearchHref,
} from '../../src/app/features/search/pages/advanced-search.logic';

const preset = (name: string, lastUsedAt?: string): SearchPreset => ({
  id: `id-${name}`,
  name,
  filters: structuredClone(EMPTY_SEARCH_FILTERS),
  createdAt: '2026-01-01T00:00:00Z',
  updatedAt: '2026-01-01T00:00:00Z',
  lastUsedAt,
  version: 1,
});

describe('dashboard logic (KTL-40)', () => {
  describe('availability split', () => {
    it('derives «sin comprobar» as what remains of the active total', () => {
      expect(availabilitySplit(10, 4, 2)).toEqual({
        total: 10,
        available: 4,
        unavailable: 2,
        unknown: 4,
      });
    });

    it('never goes below zero when the separate counts disagree', () => {
      expect(availabilitySplit(5, 4, 3).unknown).toBe(0);
      expect(inactiveCount(9, 12)).toBe(0);
      expect(inactiveCount(15, 12)).toBe(3);
    });

    it('draws no bar for an empty population and only the non-empty segments otherwise', () => {
      expect(barSegments(availabilitySplit(0, 0, 0))).toEqual([]);
      expect(barSegments(availabilitySplit(10, 4, 0))).toEqual([
        { tone: 'success', share: 0.4 },
        { tone: 'neutral', share: 0.6 },
      ]);
    });
  });

  describe('queries', () => {
    it('sends no text filter in any candidate search and bounds every page', () => {
      for (const query of Object.values(CANDIDATE_QUERIES)) {
        expect(query.text).toBe('');
        expect(query.pageSize).toBeLessThanOrEqual(DASHBOARD_ROWS);
      }
      expect(Object.keys(CANDIDATE_QUERIES)).toHaveLength(5);
    });

    it('asks for the most recently confirmed available and the newest candidates', () => {
      expect(CANDIDATE_QUERIES.recentAvailable).toMatchObject({
        availability: 'available',
        sortField: 'availabilityCheckedOn',
        sortDirection: 'desc',
        pageSize: 5,
        includeInactive: false,
      });
      expect(CANDIDATE_QUERIES.recentAdded).toMatchObject({
        availability: '',
        sortField: 'createdAt',
        sortDirection: 'desc',
        pageSize: 5,
      });
      expect(CANDIDATE_QUERIES.everyoneCount.includeInactive).toBe(true);
      expect(OPEN_POSITIONS_QUERY).toEqual({
        status: 'open',
        sortField: 'updatedAt',
        sortDirection: 'desc',
        page: 1,
        pageSize: 5,
      });
    });
  });

  describe('links', () => {
    it('leads each figure to the candidate list filtered by it', () => {
      expect(DASHBOARD_HREFS).toMatchObject({
        active: '/app/candidates',
        available: '/app/candidates?availability=available&sort=availabilityCheckedOn',
        unavailable: '/app/candidates?availability=unavailable',
        unknown: '/app/candidates?availability=unknown',
        withoutCv: '/app/candidates?cv=no',
        recentAdded: '/app/candidates?sort=createdAt',
        positions: '/app/positions',
      });
      expect(candidateHref('c-1')).toBe('/app/candidates/c-1');
      expect(positionHref('p 1')).toBe('/app/positions/p%201');
    });

    it('puts only the preset id in the search link', () => {
      const id = '0199a1b2-c3d4-7e5f-8a9b-0c1d2e3f4a5b';
      expect(presetSearchHref(id)).toBe(`/app/search?preset=${id}`);
      expect(isPresetId(id)).toBe(true);
      expect(isPresetId('abc')).toBe(false);
      expect(isPresetId(`${id}x`)).toBe(false);
    });
  });

  describe('saved searches', () => {
    it('orders by last use, never-used last, then by name, five at most', () => {
      const ordered = topPresets([
        preset('Zeta'),
        preset('Hace tres días', '2026-10-04T10:00:00Z'),
        preset('Alfa'),
        preset('Ayer', '2026-10-06T10:00:00Z'),
        preset('Beta'),
        preset('Gamma'),
      ]);

      expect(ordered.map((item) => item.name)).toEqual([
        'Ayer',
        'Hace tres días',
        'Alfa',
        'Beta',
        'Gamma',
      ]);
    });
  });
});
