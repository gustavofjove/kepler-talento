import { TranslatableError } from '../../../core/i18n/translatable-error';
import {
  Candidate,
  CandidateEducation,
  CandidateExperience,
  CandidateLanguage,
  CandidateProgram,
  CandidateSkill,
  CandidateTag,
} from '../models/candidate.models';
import { CandidateService } from './candidate.service';

const sameText = (a: string, b: string): boolean =>
  a.trim().toLowerCase() === b.trim().toLowerCase();

/**
 * The collection with `entry` in place of the one sharing its id. An id no longer present,
 * because another tab removed it, is refused rather than silently re-added.
 */
function replaceById<T extends { id: string }>(items: T[], entry: T): T[] {
  if (!items.some((item) => item.id === entry.id)) {
    throw new TranslatableError('candidate.profile.validation.entryNotFound');
  }
  return items.map((item) => (item.id === entry.id ? entry : item));
}

/**
 * Candidate relation collections.
 *
 * The validation rules are unchanged; failures are `TranslatableError`s keyed into
 * `es.json`, whose `message` is still the Spanish copy. What changed is where the result
 * is stored. Each method now awaits the API, which replaces the whole collection against
 * the candidate's concurrency token, and the methods are therefore `async`.
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
      throw new TranslatableError('candidate.profile.languages.duplicate');
    }
    const language: CandidateLanguage = { ...input, id: crypto.randomUUID() };
    await this.candidateService.setLanguages(candidateId, [...candidate.languages, language]);
  }

  /** Changes an entry in place (level, certification), keeping its id. */
  async updateLanguage(candidateId: string, language: CandidateLanguage): Promise<void> {
    const candidate = await this.require(candidateId);
    if (
      candidate.languages.some(
        (item) => item.id !== language.id && sameText(item.language, language.language),
      )
    ) {
      throw new TranslatableError('candidate.profile.languages.duplicate');
    }
    await this.candidateService.setLanguages(
      candidateId,
      replaceById(candidate.languages, language),
    );
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
      throw new TranslatableError('candidate.profile.programs.duplicate');
    }
    if (input.yearsExperience !== undefined && input.yearsExperience < 0) {
      throw new TranslatableError('candidate.profile.validation.negativeYears');
    }
    const program: CandidateProgram = { ...input, id: crypto.randomUUID() };
    await this.candidateService.setPrograms(candidateId, [...candidate.programs, program]);
  }

  /** Changes an entry in place (level, years of experience), keeping its id. */
  async updateProgram(candidateId: string, program: CandidateProgram): Promise<void> {
    const candidate = await this.require(candidateId);
    if (
      candidate.programs.some(
        (item) => item.id !== program.id && sameText(item.program, program.program),
      )
    ) {
      throw new TranslatableError('candidate.profile.programs.duplicate');
    }
    if (program.yearsExperience !== undefined && program.yearsExperience < 0) {
      throw new TranslatableError('candidate.profile.validation.negativeYears');
    }
    await this.candidateService.setPrograms(candidateId, replaceById(candidate.programs, program));
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
      throw new TranslatableError('candidate.profile.education.required');
    }
    const currentYear = new Date().getFullYear();
    if (input.endYear !== undefined && (input.endYear < 1950 || input.endYear > currentYear + 1)) {
      throw new TranslatableError('candidate.profile.education.invalidEndYear');
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
      throw new TranslatableError('candidate.profile.validation.negativeYears');
    }
    if (input.startDate && input.endDate && input.endDate < input.startDate) {
      throw new TranslatableError('candidate.profile.experience.invalidRange');
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
      throw new TranslatableError('candidate.profile.skills.duplicate');
    }
    const skill: CandidateSkill = { ...input, id: crypto.randomUUID() };
    await this.candidateService.setSkills(candidateId, [...candidate.skills, skill]);
  }

  /** Changes an entry's level in place, keeping its id. */
  async updateSkill(candidateId: string, skill: CandidateSkill): Promise<void> {
    const candidate = await this.require(candidateId);
    if (
      candidate.skills.some((item) => item.id !== skill.id && sameText(item.skill, skill.skill))
    ) {
      throw new TranslatableError('candidate.profile.skills.duplicate');
    }
    await this.candidateService.setSkills(candidateId, replaceById(candidate.skills, skill));
  }

  async removeSkill(candidateId: string, skillId: string): Promise<void> {
    const candidate = await this.require(candidateId);
    await this.candidateService.setSkills(
      candidateId,
      candidate.skills.filter((item) => item.id !== skillId),
    );
  }

  async addTag(candidateId: string, input: Omit<CandidateTag, 'id'>): Promise<void> {
    const candidate = await this.require(candidateId);
    if (candidate.tags.some((item) => sameText(item.tag, input.tag))) {
      throw new TranslatableError('candidate.profile.tags.duplicate');
    }
    const tag: CandidateTag = { ...input, id: crypto.randomUUID() };
    await this.candidateService.setTags(candidateId, [...candidate.tags, tag]);
  }

  async removeTag(candidateId: string, tagId: string): Promise<void> {
    const candidate = await this.require(candidateId);
    await this.candidateService.setTags(
      candidateId,
      candidate.tags.filter((item) => item.id !== tagId),
    );
  }

  private async require(candidateId: string): Promise<Candidate> {
    await this.candidateService.ensureAggregate(candidateId);
    const candidate = this.candidateService.find(candidateId);
    if (!candidate) {
      throw new TranslatableError('candidate.profile.validation.notFound');
    }
    return candidate;
  }
}
