import {
  Candidate,
  CandidateEducation,
  CandidateExperience,
  CandidateLanguage,
  CandidateProgram,
  CandidateSkill,
} from '../models/candidate.models';
import { CandidateService } from './candidate.service';

const sameText = (a: string, b: string): boolean =>
  a.trim().toLowerCase() === b.trim().toLowerCase();

/**
 * Candidate relation collections.
 *
 * The validation rules and their Spanish messages are unchanged; what changed is where
 * the result is stored. Each method now awaits the API, which replaces the whole
 * collection against the candidate's concurrency token, and the methods are therefore
 * `async`.
 *
 * Every method loads the aggregate before reading it. These are only ever invoked from
 * the detail screen, which has already awaited it — but "only ever" is an invariant that
 * erodes, and `ensureAggregate` is idempotent and shares an in-flight request, so paying
 * for the guarantee costs nothing.
 */
export class CandidateRelationsService {
  constructor(private readonly candidateService: CandidateService) {}

  async addLanguage(candidateId: string, input: Omit<CandidateLanguage, 'id'>): Promise<void> {
    const candidate = await this.require(candidateId);
    if (candidate.languages.some((item) => sameText(item.language, input.language))) {
      throw new Error('El candidato ya tiene este idioma registrado.');
    }
    const language: CandidateLanguage = { ...input, id: crypto.randomUUID() };
    await this.candidateService.setLanguages(candidateId, [...candidate.languages, language]);
  }

  async removeLanguage(candidateId: string, languageId: string): Promise<void> {
    const candidate = await this.require(candidateId);
    await this.candidateService.setLanguages(
      candidateId,
      candidate.languages.filter((item) => item.id !== languageId),
    );
  }

  async addProgram(candidateId: string, input: Omit<CandidateProgram, 'id'>): Promise<void> {
    const candidate = await this.require(candidateId);
    if (candidate.programs.some((item) => sameText(item.program, input.program))) {
      throw new Error('El candidato ya tiene este programa registrado.');
    }
    if (input.yearsExperience !== undefined && input.yearsExperience < 0) {
      throw new Error('Los años de experiencia no pueden ser negativos.');
    }
    const program: CandidateProgram = { ...input, id: crypto.randomUUID() };
    await this.candidateService.setPrograms(candidateId, [...candidate.programs, program]);
  }

  async removeProgram(candidateId: string, programId: string): Promise<void> {
    const candidate = await this.require(candidateId);
    await this.candidateService.setPrograms(
      candidateId,
      candidate.programs.filter((item) => item.id !== programId),
    );
  }

  async addEducation(candidateId: string, input: Omit<CandidateEducation, 'id'>): Promise<void> {
    const candidate = await this.require(candidateId);
    if (!input.degree.trim()) {
      throw new Error('La titulación es obligatoria.');
    }
    const currentYear = new Date().getFullYear();
    if (input.endYear !== undefined && (input.endYear < 1950 || input.endYear > currentYear + 1)) {
      throw new Error('El año de finalización no es válido.');
    }
    const education: CandidateEducation = { ...input, id: crypto.randomUUID() };
    await this.candidateService.setEducation(candidateId, [...candidate.education, education]);
  }

  async removeEducation(candidateId: string, educationId: string): Promise<void> {
    const candidate = await this.require(candidateId);
    await this.candidateService.setEducation(
      candidateId,
      candidate.education.filter((item) => item.id !== educationId),
    );
  }

  async addExperience(candidateId: string, input: Omit<CandidateExperience, 'id'>): Promise<void> {
    const candidate = await this.require(candidateId);
    if (input.yearsExperience !== undefined && input.yearsExperience < 0) {
      throw new Error('Los años de experiencia no pueden ser negativos.');
    }
    if (input.startDate && input.endDate && input.endDate < input.startDate) {
      throw new Error('La fecha de fin no puede ser anterior a la fecha de inicio.');
    }
    const experience: CandidateExperience = {
      ...input,
      endDate: input.isCurrent ? undefined : input.endDate,
      id: crypto.randomUUID(),
    };
    await this.candidateService.setExperience(candidateId, [...candidate.experience, experience]);
  }

  async removeExperience(candidateId: string, experienceId: string): Promise<void> {
    const candidate = await this.require(candidateId);
    await this.candidateService.setExperience(
      candidateId,
      candidate.experience.filter((item) => item.id !== experienceId),
    );
  }

  async addSkill(candidateId: string, input: Omit<CandidateSkill, 'id'>): Promise<void> {
    const candidate = await this.require(candidateId);
    if (candidate.skills.some((item) => sameText(item.skill, input.skill))) {
      throw new Error('El candidato ya tiene esta habilidad registrada.');
    }
    const skill: CandidateSkill = { ...input, id: crypto.randomUUID() };
    await this.candidateService.setSkills(candidateId, [...candidate.skills, skill]);
  }

  async removeSkill(candidateId: string, skillId: string): Promise<void> {
    const candidate = await this.require(candidateId);
    await this.candidateService.setSkills(
      candidateId,
      candidate.skills.filter((item) => item.id !== skillId),
    );
  }

  private async require(candidateId: string): Promise<Candidate> {
    await this.candidateService.ensureAggregate(candidateId);
    const candidate = this.candidateService.find(candidateId);
    if (!candidate) {
      throw new Error('Candidato no encontrado.');
    }
    return candidate;
  }
}
