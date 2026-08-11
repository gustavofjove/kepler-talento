import { CandidateListPageComponent } from '../../src/app/features/candidates/pages/candidate-list-page.component';
import { CandidateService } from '../../src/app/features/candidates/services/candidate.service';
import { EMPTY_CANDIDATE_DRAFT } from '../../src/app/features/candidates/models/candidate.models';

describe('CandidateListPageComponent', () => {
  let candidateService: CandidateService;
  let component: CandidateListPageComponent;
  const auth = { hasPermission: jest.fn().mockReturnValue(true) };
  const toast = { show: jest.fn() };
  const confirmDialog = { confirm: jest.fn().mockResolvedValue(true) };

  beforeEach(() => {
    localStorage.clear();
    candidateService = new CandidateService();
    candidateService.candidates.set([]);

    candidateService.create({
      ...EMPTY_CANDIDATE_DRAFT,
      firstName: 'Ana',
      lastName: 'Rios',
      status: 'available',
      email: 'ana@example.com',
    });
    candidateService.create({
      ...EMPTY_CANDIDATE_DRAFT,
      firstName: 'Bea',
      lastName: 'Mora',
      status: 'new',
      email: 'bea@example.com',
    });
    const inactive = candidateService.create({
      ...EMPTY_CANDIDATE_DRAFT,
      firstName: 'Carla',
      lastName: 'Gil',
      status: 'hired',
      email: 'carla@example.com',
    });
    candidateService.deactivate(inactive.id);

    component = new CandidateListPageComponent(
      candidateService,
      auth as any,
      toast as any,
      confirmDialog as any,
    );
  });

  it('filters by text and status', () => {
    component.textFilter = 'ana';
    component.statusFilter = 'available';

    const rows = component.filteredCandidates;

    expect(rows).toHaveLength(1);
    expect(rows[0].firstName).toBe('Ana');
  });

  it('excludes inactive by default and includes them when selected', () => {
    expect(component.filteredCandidates.some((item) => item.lastName === 'Gil')).toBe(false);

    component.includeInactive = true;

    expect(component.filteredCandidates.some((item) => item.lastName === 'Gil')).toBe(true);
  });

  it('deactivates selected candidates in bulk with confirmation', async () => {
    const ids = candidateService.list(true).map((item) => item.id);
    component.toggleSelected(ids[0], true);
    component.toggleSelected(ids[1], true);

    await component.bulkDeactivate();

    expect(candidateService.find(ids[0])?.isActive).toBe(false);
    expect(candidateService.find(ids[1])?.isActive).toBe(false);
    expect(toast.show).toHaveBeenCalledWith(
      expect.stringMatching(/Baja lógica aplicada/),
      'success',
    );
    expect(confirmDialog.confirm).toHaveBeenCalled();
  });

  it('reactivates selected candidates in bulk with confirmation', async () => {
    const inactive = candidateService.list(true).find((item) => !item.isActive)!;
    component.toggleSelected(inactive.id, true);

    await component.bulkReactivate();

    expect(candidateService.find(inactive.id)?.isActive).toBe(true);
    expect(toast.show).toHaveBeenCalledWith('Alta lógica aplicada a 1 candidato(s).', 'success');
  });

  it('builds active filter chips and removes them correctly', () => {
    component.textFilter = 'ana';
    component.statusFilter = 'available';
    component.hasCvFilter = 'no';
    component.includeInactive = true;

    const chipKeys = component.activeFilterChips.map((chip) => chip.key);
    expect(chipKeys).toEqual(['text', 'status', 'hasCv', 'includeInactive']);

    component.removeFilter('text');
    component.removeFilter('status');
    component.removeFilter('hasCv');
    component.removeFilter('includeInactive');

    expect(component.textFilter).toBe('');
    expect(component.statusFilter).toBe('');
    expect(component.hasCvFilter).toBe('');
    expect(component.includeInactive).toBe(false);
  });
});
