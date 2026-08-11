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

  it('logically deactivates multiple active candidates and returns updated count', () => {
    const one = service.create({
      ...EMPTY_CANDIDATE_DRAFT,
      firstName: 'Ana',
      lastName: 'Rios',
    });
    const two = service.create({
      ...EMPTY_CANDIDATE_DRAFT,
      firstName: 'Bea',
      lastName: 'Mora',
    });
    const three = service.create({
      ...EMPTY_CANDIDATE_DRAFT,
      firstName: 'Carla',
      lastName: 'Gil',
    });
    service.deactivate(three.id);

    const updated = service.deactivateMany([one.id, two.id, three.id]);

    expect(updated).toBe(2);
    expect(service.find(one.id)?.isActive).toBe(false);
    expect(service.find(two.id)?.isActive).toBe(false);
    expect(service.find(three.id)?.isActive).toBe(false);
  });

  it('reverts a logical deletion so the candidate is listed again', () => {
    const candidate = service.create({
      ...EMPTY_CANDIDATE_DRAFT,
      firstName: 'Eva',
      lastName: 'Soler',
    });
    service.deactivate(candidate.id);

    service.reactivate(candidate.id);

    expect(service.find(candidate.id)?.isActive).toBe(true);
    expect(service.list().map((item) => item.id)).toContain(candidate.id);
  });

  it('reactivates only the inactive candidates and returns updated count', () => {
    const active = service.create({
      ...EMPTY_CANDIDATE_DRAFT,
      firstName: 'Ana',
      lastName: 'Rios',
    });
    const inactive = service.create({
      ...EMPTY_CANDIDATE_DRAFT,
      firstName: 'Bea',
      lastName: 'Mora',
    });
    service.deactivate(inactive.id);

    const updated = service.reactivateMany([active.id, inactive.id]);

    expect(updated).toBe(1);
    expect(service.find(active.id)?.isActive).toBe(true);
    expect(service.find(inactive.id)?.isActive).toBe(true);
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
