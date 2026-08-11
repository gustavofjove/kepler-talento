import { CatalogService } from '../../src/app/features/catalogs/services/catalog.service';
import { CandidateService } from '../../src/app/features/candidates/services/candidate.service';
import { EMPTY_CANDIDATE_DRAFT } from '../../src/app/features/candidates/models/candidate.models';

describe('CatalogService', () => {
  let service: CatalogService;
  let candidateService: CandidateService;

  beforeEach(() => {
    localStorage.clear();
    candidateService = new CandidateService();
    service = new CatalogService(candidateService);
  });

  it('creates a catalog item and exposes it in active names', () => {
    service.create('language', 'Neerlandes');

    expect(service.activeNames('language')).toContain('Neerlandes');
  });

  it('updates catalog item name and code', () => {
    const created = service.create('skill', 'Negociacion');

    const updated = service.update('skill', created.id, {
      nameEs: 'Negociacion avanzada',
      code: 'NEG_AVZ',
    });

    expect(updated.nameEs).toBe('Negociacion avanzada');
    expect(updated.code).toBe('NEG_AVZ');
  });

  it('toggles active state and hides inactive items from active names', () => {
    const created = service.create('program', 'Python');

    service.toggleActive('program', created.id);

    expect(service.activeNames('program')).not.toContain('Python');
  });

  it('moves item in sort order', () => {
    service.create('program_level', 'Experto');
    const second = service.create('program_level', 'Senior');

    const initialIndex = service
      .list('program_level', true)
      .findIndex((item) => item.id === second.id);

    service.move('program_level', second.id, -1);

    const items = service.list('program_level', true);
    const secondIndex = items.findIndex((item) => item.id === second.id);
    expect(secondIndex).toBe(initialIndex - 1);
  });

  it('removes an existing catalog item', () => {
    const created = service.create('sector', 'Logistica');

    service.remove('sector', created.id);

    expect(service.list('sector', true).some((item) => item.id === created.id)).toBe(false);
  });

  it('prevents removing a catalog value that is used by candidates', () => {
    const candidate = candidateService.create({
      ...EMPTY_CANDIDATE_DRAFT,
      firstName: 'Ana',
      lastName: 'Perez',
    });
    candidateService.setLanguages(candidate.id, [{ id: 'l1', language: 'Inglés', level: 'B2' }]);

    const inUseLanguage = service.list('language', true).find((item) => item.nameEs === 'Inglés');
    expect(inUseLanguage).toBeTruthy();

    expect(() => service.remove('language', inUseLanguage!.id)).toThrow(/en uso por candidatos/i);
  });

  it('prevents deactivating a catalog value that is used by candidates', () => {
    const candidate = candidateService.create({
      ...EMPTY_CANDIDATE_DRAFT,
      firstName: 'Bea',
      lastName: 'Santos',
    });
    candidateService.setSkills(candidate.id, [{ id: 's1', skill: 'Análisis', level: 'Alto' }]);

    const inUseSkill = service.list('skill', true).find((item) => item.nameEs === 'Análisis');
    expect(inUseSkill).toBeTruthy();

    expect(() => service.toggleActive('skill', inUseSkill!.id)).toThrow(/en uso por candidatos/i);
  });
});
