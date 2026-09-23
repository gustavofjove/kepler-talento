import { i18n } from '../../src/app/core/i18n/i18n';
import * as logic from '../../src/app/features/candidates/pages/candidate-list.logic';
import {
  buildFilterChips,
  type CandidateFilters,
  DEFAULT_PAGE_SIZE,
  EMPTY_FILTERS,
  nextSort,
  readListView,
  removeFilter,
  toListQuery,
  writeListView,
} from '../../src/app/features/candidates/pages/candidate-list.logic';

const t = i18n.t.bind(i18n);

describe('candidate list logic', () => {
  it('keeps no candidate filtering, sorting or paging helper', () => {
    const exported = Object.keys(logic);
    for (const removed of ['filterCandidates', 'sortCandidates', 'paginate', 'totalPages']) {
      expect(exported).not.toContain(removed);
    }
  });

  describe('URL state', () => {
    it('reads a URL with page, sort and filters into that view', () => {
      const view = readListView(
        new URLSearchParams(
          'page=3&pageSize=50&sort=lastName&dir=desc&status=hired&cv=yes&inactive=1',
        ),
      );

      expect(view).toEqual({
        filters: {
          textFilter: '',
          statusFilter: 'hired',
          hasCvFilter: 'yes',
          includeInactive: true,
        },
        sort: { field: 'lastName', direction: 'desc' },
        page: 3,
        pageSize: 50,
      });
    });

    it('omits every default, so the plain list URL stays empty', () => {
      const view = readListView(new URLSearchParams());

      expect(writeListView(view).toString()).toBe('');
      expect(view.pageSize).toBe(DEFAULT_PAGE_SIZE);
      expect(view.sort).toEqual({ field: 'updatedAt', direction: 'desc' });
    });

    it('round-trips a non-default view', () => {
      const params = 'status=new&cv=no&sort=status&page=2';
      expect(writeListView(readListView(new URLSearchParams(params))).toString()).toBe(params);
    });

    it.each(['0', '-2', 'abc', '1.5', ''])(
      'falls back to page 1 for a malformed page "%s"',
      (page) => {
        expect(readListView(new URLSearchParams({ page })).page).toBe(1);
      },
    );

    it.each(['0', '101', 'x'])('falls back to the default page size for "%s"', (pageSize) => {
      expect(readListView(new URLSearchParams({ pageSize })).pageSize).toBe(DEFAULT_PAGE_SIZE);
    });

    it('ignores a status or CV value that is not one', () => {
      const view = readListView(new URLSearchParams('status=archived&cv=maybe'));
      expect(view.filters.statusFilter).toBe('');
      expect(view.filters.hasCvFilter).toBe('');
    });

    it('keeps an unknown sort field so the API can refuse it', () => {
      expect(toListQuery(readListView(new URLSearchParams('sort=email'))).sortField).toBe('email');
    });

    it('never writes the free-text filter to the URL', () => {
      const view = readListView(new URLSearchParams(), 'Marta Ruiz');

      expect(writeListView(view).toString()).not.toContain('Marta');
      expect(toListQuery(view).text).toBe('Marta Ruiz');
    });
  });

  it('flips direction on the same field and uses the natural direction on a new one', () => {
    expect(nextSort({ field: 'lastName', direction: 'asc' }, 'lastName').direction).toBe('desc');
    expect(nextSort({ field: 'lastName', direction: 'asc' }, 'updatedAt')).toEqual({
      field: 'updatedAt',
      direction: 'desc',
    });
  });

  it('builds translated filter chips with the status label and removes them correctly', () => {
    let filters: CandidateFilters = {
      textFilter: 'ana',
      statusFilter: 'in_process',
      hasCvFilter: 'no',
      includeInactive: true,
    };

    expect(buildFilterChips(filters, t)).toEqual([
      { key: 'text', label: 'Texto: ana' },
      { key: 'status', label: 'Estado: En proceso' },
      { key: 'hasCv', label: 'CV: Sin CV' },
      { key: 'includeInactive', label: 'Incluye inactivos' },
    ]);

    for (const key of ['text', 'status', 'hasCv', 'includeInactive']) {
      filters = removeFilter(filters, key);
    }
    expect(filters).toEqual(EMPTY_FILTERS);
  });
});
