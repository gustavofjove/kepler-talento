import { CandidateService } from '../../src/app/features/candidates/services/candidate.service';
import { CandidateRelationsService } from '../../src/app/features/candidates/services/candidate-relations.service';
import { EMPTY_CANDIDATE_DRAFT } from '../../src/app/features/candidates/models/candidate.models';

describe('CandidateRelationsService', () => {
  let candidateService: CandidateService;
  let relations: CandidateRelationsService;
  let candidateId: string;

  beforeEach(() => {
    localStorage.clear();
    candidateService = new CandidateService();
    relations = new CandidateRelationsService(candidateService);
    candidateId = candidateService.create({
      ...EMPTY_CANDIDATE_DRAFT,
      firstName: 'Ana',
      lastName: 'Lopez',
    }).id;
  });

  describe('languages', () => {
    it('adds a language to the candidate', () => {
      relations.addLanguage(candidateId, { language: 'Ingles', level: 'B2' });
      expect(candidateService.find(candidateId)?.languages).toHaveLength(1);
    });

    it('rejects a duplicate language regardless of case', () => {
      relations.addLanguage(candidateId, { language: 'Ingles', level: 'B2' });
      expect(() => relations.addLanguage(candidateId, { language: 'ingles', level: 'C1' })).toThrow(
        /ya tiene este idioma/i,
      );
    });

    it('removes a language by id', () => {
      relations.addLanguage(candidateId, { language: 'Frances', level: 'B1' });
      const [language] = candidateService.find(candidateId)!.languages;
      relations.removeLanguage(candidateId, language.id);
      expect(candidateService.find(candidateId)?.languages).toHaveLength(0);
    });
  });

  describe('programs', () => {
    it('rejects a duplicate program', () => {
      relations.addProgram(candidateId, {
        program: 'Excel',
        level: 'Avanzado',
        yearsExperience: 3,
      });
      expect(() => relations.addProgram(candidateId, { program: 'Excel', level: 'Medio' })).toThrow(
        /ya tiene este programa/i,
      );
    });

    it('rejects negative years of experience', () => {
      expect(() =>
        relations.addProgram(candidateId, { program: 'SAP', level: 'Medio', yearsExperience: -1 }),
      ).toThrow(/no pueden ser negativos/i);
    });
  });

  describe('education', () => {
    it('requires a degree', () => {
      expect(() =>
        relations.addEducation(candidateId, {
          educationType: 'Grado',
          degree: '   ',
          institution: 'UCM',
          status: 'Finalizada',
        }),
      ).toThrow(/titulacion es obligatoria/i);
    });

    it('rejects an implausible end year', () => {
      expect(() =>
        relations.addEducation(candidateId, {
          educationType: 'Grado',
          degree: 'Grado en ADE',
          institution: 'UCM',
          status: 'Finalizada',
          endYear: 1900,
        }),
      ).toThrow(/ano de finalizacion no es valido/i);
    });

    it('accepts a valid education record', () => {
      relations.addEducation(candidateId, {
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
    it('rejects an end date before the start date', () => {
      expect(() =>
        relations.addExperience(candidateId, {
          company: 'Acme',
          position: 'Analista',
          sector: 'Servicios',
          startDate: '2024-06-01',
          endDate: '2024-01-01',
          isCurrent: false,
        }),
      ).toThrow(/no puede ser anterior/i);
    });

    it('clears the end date when marked as current', () => {
      relations.addExperience(candidateId, {
        company: 'Acme',
        position: 'Analista',
        sector: 'Servicios',
        startDate: '2024-01-01',
        endDate: '2024-06-01',
        isCurrent: true,
      });
      expect(candidateService.find(candidateId)?.experience[0].endDate).toBeUndefined();
    });

    it('rejects negative years of experience', () => {
      expect(() =>
        relations.addExperience(candidateId, {
          company: 'Acme',
          position: 'Analista',
          sector: 'Servicios',
          isCurrent: false,
          yearsExperience: -2,
        }),
      ).toThrow(/no pueden ser negativos/i);
    });
  });

  describe('skills', () => {
    it('rejects a duplicate skill', () => {
      relations.addSkill(candidateId, { skill: 'Gestion documental', level: 'Alto' });
      expect(() =>
        relations.addSkill(candidateId, { skill: 'gestion documental', level: 'Medio' }),
      ).toThrow(/ya tiene esta habilidad/i);
    });

    it('removes a skill by id', () => {
      relations.addSkill(candidateId, { skill: 'Compras', level: 'Medio' });
      const [skill] = candidateService.find(candidateId)!.skills;
      relations.removeSkill(candidateId, skill.id);
      expect(candidateService.find(candidateId)?.skills).toHaveLength(0);
    });
  });

  it('throws when the candidate does not exist', () => {
    expect(() => relations.addLanguage('missing-id', { language: 'Ingles', level: 'B1' })).toThrow(
      /no encontrado/i,
    );
  });
});
