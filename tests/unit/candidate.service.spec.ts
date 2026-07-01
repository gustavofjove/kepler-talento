import { CandidateService } from '../../src/app/features/candidates/services/candidate.service';
import { EMPTY_CANDIDATE_DRAFT } from '../../src/app/features/candidates/models/candidate.models';

describe('CandidateService', () => {
  let service: CandidateService;

  beforeEach(() => {
    localStorage.clear();
    service = new CandidateService();
  });

  it('creates a candidate with audit timestamps and empty relations', () => {
    const candidate = service.create({
      ...EMPTY_CANDIDATE_DRAFT,
      firstName: 'Maria',
      lastName: 'Ruiz',
    });

    expect(candidate.id).toBeTruthy();
    expect(candidate.isActive).toBe(true);
    expect(candidate.createdAt).toBe(candidate.updatedAt);
    expect(candidate.languages).toEqual([]);
    expect(service.find(candidate.id)).toEqual(candidate);
  });

  it('lists only active candidates by default', () => {
    const active = service.create({
      ...EMPTY_CANDIDATE_DRAFT,
      firstName: 'Activa',
      lastName: 'Uno',
    });
    const inactive = service.create({
      ...EMPTY_CANDIDATE_DRAFT,
      firstName: 'Inactiva',
      lastName: 'Dos',
    });
    service.deactivate(inactive.id);

    const visible = service.list().map((candidate) => candidate.id);

    expect(visible).toContain(active.id);
    expect(visible).not.toContain(inactive.id);
    expect(service.list(true).map((candidate) => candidate.id)).toContain(inactive.id);
  });

  it('updates a candidate and refreshes updatedAt without losing relations', () => {
    const candidate = service.create({
      ...EMPTY_CANDIDATE_DRAFT,
      firstName: 'Luis',
      lastName: 'Diaz',
    });

    const updated = service.update(candidate.id, {
      ...EMPTY_CANDIDATE_DRAFT,
      firstName: 'Luis',
      lastName: 'Diaz Garcia',
    });

    expect(updated.lastName).toBe('Diaz Garcia');
    expect(updated.id).toBe(candidate.id);
  });

  it('throws when updating a candidate that does not exist', () => {
    expect(() => service.update('missing-id', EMPTY_CANDIDATE_DRAFT)).toThrow(/no encontrado/i);
  });

  it('logically deactivates a candidate without deleting it', () => {
    const candidate = service.create({
      ...EMPTY_CANDIDATE_DRAFT,
      firstName: 'Eva',
      lastName: 'Soler',
    });

    service.deactivate(candidate.id);

    const stored = service.find(candidate.id);
    expect(stored?.isActive).toBe(false);
    expect(stored).toBeDefined();
  });

  it('marks only the newest document as primary when adding a primary CV', () => {
    const candidate = service.create({
      ...EMPTY_CANDIDATE_DRAFT,
      firstName: 'Noa',
      lastName: 'Vidal',
    });
    service.addDocument(candidate.id, {
      id: 'd1',
      documentType: 'CV',
      originalFilename: 'cv1.pdf',
      mimeType: 'application/pdf',
      sizeBytes: 100,
      isPrimary: true,
      uploadedAt: new Date().toISOString(),
    });

    service.addDocument(candidate.id, {
      id: 'd2',
      documentType: 'CV',
      originalFilename: 'cv2.pdf',
      mimeType: 'application/pdf',
      sizeBytes: 200,
      isPrimary: true,
      uploadedAt: new Date().toISOString(),
    });

    const documents = service.find(candidate.id)?.documents ?? [];
    expect(documents.filter((document) => document.isPrimary)).toHaveLength(1);
    expect(documents.find((document) => document.id === 'd2')?.isPrimary).toBe(true);
  });
});
