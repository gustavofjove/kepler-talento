import { CandidateService } from '../../src/app/features/candidates/services/candidate.service';
import { CandidateRelationsService } from '../../src/app/features/candidates/services/candidate-relations.service';
import { EMPTY_CANDIDATE_DRAFT } from '../../src/app/features/candidates/models/candidate.models';
import { createCandidateTestBed, FakeCandidateApi } from './support/candidate-doubles';

describe('CandidateRelationsService', () => {
  let candidateService: CandidateService;
  let api: FakeCandidateApi;
  let relations: CandidateRelationsService;
  let candidateId: string;

  beforeEach(async () => {
    localStorage.clear();
    ({ service: candidateService, api } = createCandidateTestBed());
    relations = new CandidateRelationsService(candidateService);
    await candidateService.ensureLoaded();
    candidateId = (
      await candidateService.create({
        ...EMPTY_CANDIDATE_DRAFT,
        firstName: 'Ana',
        lastName: 'Lopez',
      })
    ).id;
  });

  describe('languages', () => {
    it('adds a language to the candidate', async () => {
      await relations.addLanguage(candidateId, { language: 'Inglés', level: 'B2' });
      expect(candidateService.find(candidateId)?.languages).toHaveLength(1);
    });

    it('rejects a duplicate language regardless of case', async () => {
      await relations.addLanguage(candidateId, { language: 'Inglés', level: 'B2' });
      await expect(
        relations.addLanguage(candidateId, { language: 'inglés', level: 'C1' }),
      ).rejects.toThrow(/ya tiene este idioma/i);
    });

    it('removes a language by id', async () => {
      await relations.addLanguage(candidateId, { language: 'Francés', level: 'B1' });
      const [language] = candidateService.find(candidateId)!.languages;
      await relations.removeLanguage(candidateId, language.id);
      expect(candidateService.find(candidateId)?.languages).toHaveLength(0);
    });
  });

  describe('programs', () => {
    it('rejects a duplicate program', async () => {
      await relations.addProgram(candidateId, {
        program: 'Excel',
        level: 'Avanzado',
        yearsExperience: 3,
      });
      await expect(
        relations.addProgram(candidateId, { program: 'Excel', level: 'Medio' }),
      ).rejects.toThrow(/ya tiene este programa/i);
    });

    it('rejects negative years of experience', async () => {
      await expect(
        relations.addProgram(candidateId, { program: 'SAP', level: 'Medio', yearsExperience: -1 }),
      ).rejects.toThrow(/no pueden ser negativos/i);
    });
  });

  describe('education', () => {
    it('requires a degree', async () => {
      await expect(
        relations.addEducation(candidateId, {
          educationType: 'Grado',
          degree: '   ',
          institution: 'UCM',
          status: 'Finalizada',
        }),
      ).rejects.toThrow(/titulación es obligatoria/i);
    });

    it('rejects an implausible end year', async () => {
      await expect(
        relations.addEducation(candidateId, {
          educationType: 'Grado',
          degree: 'Grado en ADE',
          institution: 'UCM',
          status: 'Finalizada',
          endYear: 1900,
        }),
      ).rejects.toThrow(/año de finalización no es válido/i);
    });

    it('accepts a valid education record', async () => {
      await relations.addEducation(candidateId, {
        educationType: 'Grado',
        degree: 'Grado en ADE',
        institution: 'UCM',
        status: 'Finalizada',
        endYear: 2020,
      });
      expect(candidateService.find(candidateId)?.education).toHaveLength(1);
    });
  });

  describe('experience', () => {
    it('rejects an end date before the start date', async () => {
      await expect(
        relations.addExperience(candidateId, {
          company: 'Acme',
          position: 'Analista',
          sector: 'Servicios',
          startDate: '2024-06-01',
          endDate: '2024-01-01',
          isCurrent: false,
        }),
      ).rejects.toThrow(/no puede ser anterior/i);
    });

    it('clears the end date when marked as current', async () => {
      await relations.addExperience(candidateId, {
        company: 'Acme',
        position: 'Analista',
        sector: 'Servicios',
        startDate: '2024-01-01',
        endDate: '2024-06-01',
        isCurrent: true,
      });
      expect(candidateService.find(candidateId)?.experience[0].endDate).toBeUndefined();
    });

    it('rejects negative years of experience', async () => {
      await expect(
        relations.addExperience(candidateId, {
          company: 'Acme',
          position: 'Analista',
          sector: 'Servicios',
          isCurrent: false,
          yearsExperience: -2,
        }),
      ).rejects.toThrow(/no pueden ser negativos/i);
    });
  });

  describe('skills', () => {
    it('rejects a duplicate skill', async () => {
      await relations.addSkill(candidateId, { skill: 'Gestión documental', level: 'Alto' });
      await expect(
        relations.addSkill(candidateId, { skill: 'gestión documental', level: 'Medio' }),
      ).rejects.toThrow(/ya tiene esta habilidad/i);
    });

    it('removes a skill by id', async () => {
      await relations.addSkill(candidateId, { skill: 'Compras', level: 'Medio' });
      const [skill] = candidateService.find(candidateId)!.skills;
      await relations.removeSkill(candidateId, skill.id);
      expect(candidateService.find(candidateId)?.skills).toHaveLength(0);
    });
  });

  it('throws when the candidate does not exist', async () => {
    await expect(
      relations.addLanguage('missing-id', { language: 'Inglés', level: 'B1' }),
    ).rejects.toThrow(/no encontrado/i);
  });

  // The service is only ever driven from the detail screen, which has already awaited the
  // aggregate — but that is an invariant that erodes, so the service loads it itself.
  it('loads a candidate it has not seen rather than reporting it missing', async () => {
    api.seed({ id: 'never-opened', firstName: 'Nunca', lastName: 'Abierta' });

    await relations.addLanguage('never-opened', { language: 'Inglés', level: 'B1' });

    expect(candidateService.find('never-opened')?.languages).toHaveLength(1);
  });
});
