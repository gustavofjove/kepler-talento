import { CandidateService } from '../../src/app/features/candidates/services/candidate.service';
import { CandidateSearchService } from '../../src/app/features/search/services/candidate-search.service';
import { EMPTY_CANDIDATE_DRAFT } from '../../src/app/features/candidates/models/candidate.models';

describe('CandidateSearchService', () => {
  let candidateService: CandidateService;
  let search: CandidateSearchService;

  beforeEach(() => {
    localStorage.clear();
    candidateService = new CandidateService();
    candidateService.candidates.set([]);
    search = new CandidateSearchService(candidateService);

    const ana = candidateService.create({
      ...EMPTY_CANDIDATE_DRAFT,
      firstName: 'Ana',
      lastName: 'Texidor',
      status: 'available',
    });
    candidateService.setLanguages(ana.id, [
      { id: 'l1', language: 'Inglés', level: 'B2' },
      { id: 'l2', language: 'Francés', level: 'B1' },
    ]);
    candidateService.setPrograms(ana.id, [{ id: 'p1', program: 'Excel', level: 'Avanzado' }]);
    candidateService.addDocument(ana.id, {
      id: 'd1',
      documentType: 'CV',
      originalFilename: 'ana.pdf',
      mimeType: 'application/pdf',
      sizeBytes: 10,
      isPrimary: true,
      uploadedAt: new Date().toISOString(),
    });

    const bea = candidateService.create({
      ...EMPTY_CANDIDATE_DRAFT,
      firstName: 'Bea',
      lastName: 'Soriano',
      status: 'new',
    });
    candidateService.setLanguages(bea.id, [{ id: 'l3', language: 'Inglés', level: 'A2' }]);

    const inactiveCarla = candidateService.create({
      ...EMPTY_CANDIDATE_DRAFT,
      firstName: 'Carla',
      lastName: 'Mendez',
    });
    candidateService.deactivate(inactiveCarla.id);
  });

  it('returns all active candidates with empty filters', () => {
    const results = search.search(search.emptyFilters());
    expect(results.map((item) => item.lastName).sort()).toEqual(['Soriano', 'Texidor']);
  });

  it('filters by free text across name, email, phone, and notes', () => {
    const results = search.search({ ...search.emptyFilters(), text: 'texidor' });
    expect(results).toHaveLength(1);
    expect(results[0].lastName).toBe('Texidor');
  });

  it('combines status and free-text filters with AND semantics', () => {
    const results = search.search({
      ...search.emptyFilters(),
      text: 'ana',
      statusValues: ['new'],
    });
    expect(results).toHaveLength(0);
  });

  it('matches candidates when any selected status is present', () => {
    const results = search.search({
      ...search.emptyFilters(),
      statusValues: ['new', 'available'],
    });
    expect(results.map((item) => item.lastName).sort()).toEqual(['Soriano', 'Texidor']);
  });

  it('matches languages in ANY mode when at least one selected language is present', () => {
    const results = search.search({
      ...search.emptyFilters(),
      languageCriteria: [{ value: 'Francés', level: '' }],
      languageMode: 'ANY',
    });
    expect(results.map((item) => item.lastName)).toEqual(['Texidor']);
  });

  it('matches programs in ALL mode only when every selected program is present', () => {
    const results = search.search({
      ...search.emptyFilters(),
      programCriteria: [
        { value: 'Excel', level: '' },
        { value: 'SAP', level: '' },
      ],
      programMode: 'ALL',
    });
    expect(results).toHaveLength(0);
  });

  it('narrows a criterion to the exact level when one is selected', () => {
    const withB2 = search.search({
      ...search.emptyFilters(),
      languageCriteria: [{ value: 'Inglés', level: 'B2' }],
    });
    expect(withB2.map((item) => item.lastName)).toEqual(['Texidor']);

    const anyLevel = search.search({
      ...search.emptyFilters(),
      languageCriteria: [{ value: 'Inglés', level: '' }],
    });
    expect(anyLevel.map((item) => item.lastName).sort()).toEqual(['Soriano', 'Texidor']);
  });

  it('combines several criteria of the same type with ANY and ALL semantics', () => {
    const anyOf = search.search({
      ...search.emptyFilters(),
      languageCriteria: [
        { value: 'Inglés', level: 'A2' },
        { value: 'Francés', level: 'B1' },
      ],
      languageMode: 'ANY',
    });
    expect(anyOf.map((item) => item.lastName).sort()).toEqual(['Soriano', 'Texidor']);

    const allOf = search.search({
      ...search.emptyFilters(),
      languageCriteria: [
        { value: 'Inglés', level: 'B2' },
        { value: 'Francés', level: 'B1' },
      ],
      languageMode: 'ALL',
    });
    expect(allOf.map((item) => item.lastName)).toEqual(['Texidor']);
  });

  it('combines different criteria types with AND regardless of each type mode', () => {
    const results = search.search({
      ...search.emptyFilters(),
      languageCriteria: [{ value: 'Inglés', level: '' }],
      languageMode: 'ANY',
      programCriteria: [{ value: 'Excel', level: 'Avanzado' }],
      programMode: 'ANY',
    });
    expect(results.map((item) => item.lastName)).toEqual(['Texidor']);
  });

  it('filters by CV availability', () => {
    const withCv = search.search({ ...search.emptyFilters(), hasCv: 'yes' });
    expect(withCv.map((item) => item.lastName)).toEqual(['Texidor']);
    expect(withCv[0].primaryCvDocumentId).toBeTruthy();

    const withoutCv = search.search({ ...search.emptyFilters(), hasCv: 'no' });
    expect(withoutCv.map((item) => item.lastName)).toEqual(['Soriano']);
  });

  it('never returns duplicate candidates and excludes logically inactive ones', () => {
    const results = search.search(search.emptyFilters());
    const ids = results.map((item) => item.candidateId);
    expect(new Set(ids).size).toBe(ids.length);
    expect(results.map((item) => item.lastName)).not.toContain('Mendez');
  });
});
