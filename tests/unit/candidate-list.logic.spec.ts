import {
  buildFilterChips,
  type CandidateFilters,
  EMPTY_FILTERS,
  filterCandidates,
  removeFilter,
} from '../../src/app/features/candidates/pages/candidate-list.logic';
import { EMPTY_CANDIDATE_DRAFT } from '../../src/app/features/candidates/models/candidate.models';
import { CandidateService } from '../../src/app/features/candidates/services/candidate.service';
import { createCandidateTestBed } from './support/candidate-doubles';

/** Mirrors what the page does before filtering: scope by active flag. */
const scope = (service: CandidateService, filters: CandidateFilters) =>
  service.list(filters.includeInactive);

describe('candidate list logic', () => {
  let candidateService: CandidateService;

  beforeEach(async () => {
    localStorage.clear();
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

  it('filters by text and status', () => {
    const filters: CandidateFilters = {
      ...EMPTY_FILTERS,
      textFilter: 'ana',
      statusFilter: 'available',
    };

    const rows = filterCandidates(scope(candidateService, filters), filters);

    expect(rows).toHaveLength(1);
    expect(rows[0].firstName).toBe('Ana');
  });

  it('excludes inactive by default and includes them when selected', () => {
    const withoutInactive = filterCandidates(scope(candidateService, EMPTY_FILTERS), EMPTY_FILTERS);
    expect(withoutInactive.some((item) => item.lastName === 'Gil')).toBe(false);

    const filters: CandidateFilters = { ...EMPTY_FILTERS, includeInactive: true };
    const withInactive = filterCandidates(scope(candidateService, filters), filters);
    expect(withInactive.some((item) => item.lastName === 'Gil')).toBe(true);
  });

  it('builds active filter chips and removes them correctly', () => {
    let filters: CandidateFilters = {
      textFilter: 'ana',
      statusFilter: 'available',
      hasCvFilter: 'no',
      includeInactive: true,
    };

    expect(buildFilterChips(filters).map((chip) => chip.key)).toEqual([
      'text',
      'status',
      'hasCv',
      'includeInactive',
    ]);

    filters = removeFilter(filters, 'text');
    filters = removeFilter(filters, 'status');
    filters = removeFilter(filters, 'hasCv');
    filters = removeFilter(filters, 'includeInactive');

    expect(filters.textFilter).toBe('');
    expect(filters.statusFilter).toBe('');
    expect(filters.hasCvFilter).toBe('');
    expect(filters.includeInactive).toBe(false);
  });
});
