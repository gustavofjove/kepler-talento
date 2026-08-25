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

export class CandidateRelationsService {
  constructor(private readonly candidateService: CandidateService) {}

  addLanguage(candidateId: string, input: Omit<CandidateLanguage, 'id'>): void {
    const candidate = this.require(candidateId);
    if (candidate.languages.some((item) => sameText(item.language, input.language))) {
      throw new Error('El candidato ya tiene este idioma registrado.');
    }
    const language: CandidateLanguage = { ...input, id: crypto.randomUUID() };
    this.candidateService.setLanguages(candidateId, [...candidate.languages, language]);
  }

  removeLanguage(candidateId: string, languageId: string): void {
    const candidate = this.require(candidateId);
    this.candidateService.setLanguages(
      candidateId,
      candidate.languages.filter((item) => item.id !== languageId),
    );
  }

  addProgram(candidateId: string, input: Omit<CandidateProgram, 'id'>): void {
    const candidate = this.require(candidateId);
    if (candidate.programs.some((item) => sameText(item.program, input.program))) {
      throw new Error('El candidato ya tiene este programa registrado.');
    }
    if (input.yearsExperience !== undefined && input.yearsExperience < 0) {
      throw new Error('Los años de experiencia no pueden ser negativos.');
    }
    const program: CandidateProgram = { ...input, id: crypto.randomUUID() };
    this.candidateService.setPrograms(candidateId, [...candidate.programs, program]);
  }

  removeProgram(candidateId: string, programId: string): void {
    const candidate = this.require(candidateId);
    this.candidateService.setPrograms(
      candidateId,
      candidate.programs.filter((item) => item.id !== programId),
    );
  }

  addEducation(candidateId: string, input: Omit<CandidateEducation, 'id'>): void {
    const candidate = this.require(candidateId);
    if (!input.degree.trim()) {
      throw new Error('La titulación es obligatoria.');
    }
    const currentYear = new Date().getFullYear();
    if (input.endYear !== undefined && (input.endYear < 1950 || input.endYear > currentYear + 1)) {
      throw new Error('El año de finalización no es válido.');
    }
    const education: CandidateEducation = { ...input, id: crypto.randomUUID() };
    this.candidateService.setEducation(candidateId, [...candidate.education, education]);
  }

  removeEducation(candidateId: string, educationId: string): void {
    const candidate = this.require(candidateId);
    this.candidateService.setEducation(
      candidateId,
      candidate.education.filter((item) => item.id !== educationId),
    );
  }

  addExperience(candidateId: string, input: Omit<CandidateExperience, 'id'>): void {
    const candidate = this.require(candidateId);
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
    this.candidateService.setExperience(candidateId, [...candidate.experience, experience]);
  }

  removeExperience(candidateId: string, experienceId: string): void {
    const candidate = this.require(candidateId);
    this.candidateService.setExperience(
      candidateId,
      candidate.experience.filter((item) => item.id !== experienceId),
    );
  }

  addSkill(candidateId: string, input: Omit<CandidateSkill, 'id'>): void {
    const candidate = this.require(candidateId);
    if (candidate.skills.some((item) => sameText(item.skill, input.skill))) {
      throw new Error('El candidato ya tiene esta habilidad registrada.');
    }
    const skill: CandidateSkill = { ...input, id: crypto.randomUUID() };
    this.candidateService.setSkills(candidateId, [...candidate.skills, skill]);
  }

  removeSkill(candidateId: string, skillId: string): void {
    const candidate = this.require(candidateId);
    this.candidateService.setSkills(
      candidateId,
      candidate.skills.filter((item) => item.id !== skillId),
    );
  }

  private require(candidateId: string): Candidate {
    const candidate = this.candidateService.find(candidateId);
    if (!candidate) {
      throw new Error('Candidato no encontrado.');
    }
    return candidate;
  }
}
