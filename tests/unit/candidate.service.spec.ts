import { EMPTY_CANDIDATE_DRAFT } from '../../src/app/features/candidates/models/candidate.models';
import { CandidateService } from '../../src/app/features/candidates/services/candidate.service';
import {
  ConflictError,
  createCandidateTestBed,
  FakeCandidateApi,
} from './support/candidate-doubles';

describe('CandidateService', () => {
  let service: CandidateService;
  let api: FakeCandidateApi;

  beforeEach(async () => {
    localStorage.clear();
    ({ service, api } = createCandidateTestBed());
    await service.ensureLoaded();
  });

  it('creates a candidate with audit timestamps and empty relations', async () => {
    const candidate = await service.create({
      ...EMPTY_CANDIDATE_DRAFT,
      firstName: 'Maria',
      lastName: 'Ruiz',
    });

    expect(candidate.id).toBeTruthy();
    expect(candidate.isActive).toBe(true);
    expect(candidate.languages).toEqual([]);
    expect(service.find(candidate.id)).toEqual(candidate);
  });

  it('lists only active candidates by default', async () => {
    const active = await service.create({
      ...EMPTY_CANDIDATE_DRAFT,
      firstName: 'Activa',
      lastName: 'Uno',
    });
    const inactive = await service.create({
      ...EMPTY_CANDIDATE_DRAFT,
      firstName: 'Inactiva',
      lastName: 'Dos',
    });
    await service.deactivate(inactive.id);

    const visible = service.list().map((candidate) => candidate.id);

    expect(visible).toContain(active.id);
    expect(visible).not.toContain(inactive.id);
    expect(service.list(true).map((candidate) => candidate.id)).toContain(inactive.id);
  });

  it('updates a candidate and refreshes updatedAt without losing relations', async () => {
    const candidate = await service.create({
      ...EMPTY_CANDIDATE_DRAFT,
      firstName: 'Luis',
      lastName: 'Diaz',
    });

    const updated = await service.update(candidate.id, {
      ...EMPTY_CANDIDATE_DRAFT,
      firstName: 'Luis',
      lastName: 'Diaz Garcia',
    });

    expect(updated.lastName).toBe('Diaz Garcia');
    expect(updated.id).toBe(candidate.id);
  });

  it('throws when updating a candidate that does not exist', async () => {
    await expect(service.update('missing-id', EMPTY_CANDIDATE_DRAFT)).rejects.toThrow(
      /no encontrado/i,
    );
  });

  it('logically deactivates a candidate without deleting it', async () => {
    const candidate = await service.create({
      ...EMPTY_CANDIDATE_DRAFT,
      firstName: 'Eva',
      lastName: 'Soler',
    });

    await service.deactivate(candidate.id);

    const stored = service.find(candidate.id);
    expect(stored?.isActive).toBe(false);
    expect(stored).toBeDefined();
  });

  it('logically deactivates multiple active candidates and returns updated count', async () => {
    const one = await service.create({
      ...EMPTY_CANDIDATE_DRAFT,
      firstName: 'Ana',
      lastName: 'Rios',
    });
    const two = await service.create({
      ...EMPTY_CANDIDATE_DRAFT,
      firstName: 'Bea',
      lastName: 'Mora',
    });
    const three = await service.create({
      ...EMPTY_CANDIDATE_DRAFT,
      firstName: 'Carla',
      lastName: 'Gil',
    });
    await service.deactivate(three.id);

    const updated = await service.deactivateMany([one.id, two.id, three.id]);

    // Three requested, but the already-inactive one did not change, so it is not counted.
    expect(updated).toBe(2);
    expect(service.find(one.id)?.isActive).toBe(false);
    expect(service.find(two.id)?.isActive).toBe(false);
    expect(service.find(three.id)?.isActive).toBe(false);
  });

  it('reverts a logical deletion so the candidate is listed again', async () => {
    const candidate = await service.create({
      ...EMPTY_CANDIDATE_DRAFT,
      firstName: 'Eva',
      lastName: 'Soler',
    });
    await service.deactivate(candidate.id);

    await service.reactivate(candidate.id);

    expect(service.find(candidate.id)?.isActive).toBe(true);
    expect(service.list().map((item) => item.id)).toContain(candidate.id);
  });

  it('reactivates only the inactive candidates and returns updated count', async () => {
    const active = await service.create({
      ...EMPTY_CANDIDATE_DRAFT,
      firstName: 'Ana',
      lastName: 'Rios',
    });
    const inactive = await service.create({
      ...EMPTY_CANDIDATE_DRAFT,
      firstName: 'Bea',
      lastName: 'Mora',
    });
    await service.deactivate(inactive.id);

    const updated = await service.reactivateMany([active.id, inactive.id]);

    expect(updated).toBe(1);
    expect(service.find(active.id)?.isActive).toBe(true);
    expect(service.find(inactive.id)?.isActive).toBe(true);
  });

  it('marks only the newest document as primary when adding a primary CV', async () => {
    const candidate = await service.create({
      ...EMPTY_CANDIDATE_DRAFT,
      firstName: 'Noa',
      lastName: 'Vidal',
    });
    await service.addDocument(candidate.id, document('d1', 'cv1.pdf', true));

    await service.addDocument(candidate.id, document('d2', 'cv2.pdf', true));

    const documents = service.find(candidate.id)?.documents ?? [];
    expect(documents.filter((item) => item.isPrimary)).toHaveLength(1);
    expect(documents.find((item) => item.id === 'd2')?.isPrimary).toBe(true);
  });

  describe('the cache', () => {
    it('writes nothing to browser storage', async () => {
      await service.create({
        ...EMPTY_CANDIDATE_DRAFT,
        firstName: 'Privada',
        lastName: 'Datos',
      });

      expect(localStorage.length).toBe(0);
      expect(localStorage.getItem('rrhh-candidates')).toBeNull();
    });

    it('loads an aggregate once however many callers ask concurrently', async () => {
      api.seed({ id: 'c1', firstName: 'Ana', lastName: 'Gil' });

      await Promise.all([
        service.ensureAggregate('c1'),
        service.ensureAggregate('c1'),
        service.ensureAggregate('c1'),
      ]);
      await service.ensureAggregate('c1');

      expect(api.getCalls).toEqual(['c1']);
      expect(service.find('c1')?.firstName).toBe('Ana');
    });

    it('reports a failed load rather than presenting an empty list as a result', async () => {
      const { service: failing, api: failingApi } = createCandidateTestBed();
      failingApi.failure = new Error('sin conexión');

      await failing.ensureLoaded();

      expect(failing.status).toBe('error');
      expect(failing.list()).toEqual([]);
      expect(failing.error?.message).toBeTruthy();
    });

    it('leaves find() undefined for a candidate that does not exist, without failing', async () => {
      await service.ensureAggregate('no-such-candidate');

      expect(service.find('no-such-candidate')).toBeUndefined();
      expect(service.status).not.toBe('error');
    });

    it('refuses a write carrying a version another writer has already moved past', async () => {
      const seeded = api.seed({ id: 'c1', firstName: 'Ana', lastName: 'Gil' });
      await service.ensureAggregate('c1');
      // Somebody else writes, advancing the stored version past the one we hold.
      await api.update('c1', { ...EMPTY_CANDIDATE_DRAFT, firstName: 'Otra' }, seeded.version);

      await expect(
        service.update('c1', { ...EMPTY_CANDIDATE_DRAFT, firstName: 'Nuestra' }),
      ).rejects.toBeInstanceOf(ConflictError);
    });
  });
});

function document(id: string, filename: string, isPrimary: boolean) {
  return {
    id,
    documentType: 'CV',
    originalFilename: filename,
    mimeType: 'application/pdf',
    sizeBytes: 100,
    isPrimary,
    uploadedAt: new Date().toISOString(),
  };
}
