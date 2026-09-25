import { TranslatableError } from '../../../core/i18n/translatable-error';
import {
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

/** True when another entry of `list` (not `entry` itself, by id) holds the same value. */
function duplicates<T extends { id: string }>(
  list: readonly T[],
  entry: T,
  value: (item: T) => string,
): boolean {
  return list.some((item) => item.id !== entry.id && sameText(value(item), value(entry)));
}

function refuseNegativeYears(years: number | undefined): void {
  if (years !== undefined && years < 0) {
    throw new TranslatableError('candidate.profile.validation.negativeYears');
  }
}

/*
 * Per-entry validators (KTL-29 design D3). Each checks one entry against the list it is
 * about to join or already belongs to, so a panel can refuse a bad entry before it enters
 * its draft, and a save can re-check the whole list with exactly the same rules.
 */

export function validateLanguageEntry(
  list: readonly CandidateLanguage[],
  entry: CandidateLanguage,
): void {
  if (duplicates(list, entry, (item) => item.language)) {
    throw new TranslatableError('candidate.profile.languages.duplicate');
  }
}

export function validateProgramEntry(
  list: readonly CandidateProgram[],
  entry: CandidateProgram,
): void {
  if (duplicates(list, entry, (item) => item.program)) {
    throw new TranslatableError('candidate.profile.programs.duplicate');
  }
  refuseNegativeYears(entry.yearsExperience);
}

export function validateSkillEntry(list: readonly CandidateSkill[], entry: CandidateSkill): void {
  if (duplicates(list, entry, (item) => item.skill)) {
    throw new TranslatableError('candidate.profile.skills.duplicate');
  }
}

export function validateTagEntry(list: readonly CandidateTag[], entry: CandidateTag): void {
  if (duplicates(list, entry, (item) => item.tag)) {
    throw new TranslatableError('candidate.profile.tags.duplicate');
  }
}

/**
 * Refuses a blank required field. The panel forms use `noValidate`, so the browser does not
 * enforce their `required` attributes: this is the check. The API and the database require
 * the same fields.
 */
function requireText(value: string | undefined, key: string): void {
  if (!value?.trim()) throw new TranslatableError(key);
}

export function validateEducationEntry(entry: CandidateEducation): void {
  // In form order, so the first message names the first field to fill in.
  requireText(entry.educationType, 'candidate.profile.education.typeRequired');
  requireText(entry.degree, 'candidate.profile.education.required');
  requireText(entry.institution, 'candidate.profile.education.institutionRequired');
  requireText(entry.status, 'candidate.profile.education.statusRequired');
  const currentYear = new Date().getFullYear();
  if (entry.endYear !== undefined && (entry.endYear < 1950 || entry.endYear > currentYear + 1)) {
    throw new TranslatableError('candidate.profile.education.invalidEndYear');
  }
}

export function validateExperienceEntry(entry: CandidateExperience): void {
  requireText(entry.company, 'candidate.profile.experience.companyRequired');
  requireText(entry.position, 'candidate.profile.experience.positionRequired');
  requireText(entry.sector, 'candidate.profile.experience.sectorRequired');
  refuseNegativeYears(entry.yearsExperience);
  if (entry.startDate && entry.endDate && entry.endDate < entry.startDate) {
    throw new TranslatableError('candidate.profile.experience.invalidRange');
  }
}

/** A current position has no end date; the draft may still hold one from before the tick. */
export function normalizeExperience(entry: CandidateExperience): CandidateExperience {
  return { ...entry, endDate: entry.isCurrent ? undefined : entry.endDate };
}

/**
 * Candidate relation collections.
 *
 * Since KTL-29 each panel edits a draft and saves the whole list at once, which is exactly
 * what the API accepts: every collection is replaced as a set against the candidate's
 * concurrency token. Each save re-validates the complete list with the per-entry rules
 * above; failures are `TranslatableError`s keyed into `es.json`.
 *
 * Every method loads the aggregate first. `ensureAggregate` is idempotent and shares an
 * in-flight request, so it costs nothing on the candidate page, which has already awaited it,
 * and it guarantees the version the write carries.
 */
export class CandidateRelationsService {
  constructor(private readonly candidateService: CandidateService) {}

  async saveLanguages(candidateId: string, entries: CandidateLanguage[]): Promise<void> {
    entries.forEach((entry) => validateLanguageEntry(entries, entry));
    await this.require(candidateId);
    await this.candidateService.setLanguages(candidateId, entries);
  }

  async savePrograms(candidateId: string, entries: CandidateProgram[]): Promise<void> {
    entries.forEach((entry) => validateProgramEntry(entries, entry));
    await this.require(candidateId);
    await this.candidateService.setPrograms(candidateId, entries);
  }

  async saveSkills(candidateId: string, entries: CandidateSkill[]): Promise<void> {
    entries.forEach((entry) => validateSkillEntry(entries, entry));
    await this.require(candidateId);
    await this.candidateService.setSkills(candidateId, entries);
  }

  async saveTags(candidateId: string, entries: CandidateTag[]): Promise<void> {
    entries.forEach((entry) => validateTagEntry(entries, entry));
    await this.require(candidateId);
    await this.candidateService.setTags(candidateId, entries);
  }

  async saveEducation(candidateId: string, entries: CandidateEducation[]): Promise<void> {
    entries.forEach(validateEducationEntry);
    await this.require(candidateId);
    await this.candidateService.setEducation(candidateId, entries);
  }

  async saveExperience(candidateId: string, entries: CandidateExperience[]): Promise<void> {
    entries.forEach(validateExperienceEntry);
    await this.require(candidateId);
    await this.candidateService.setExperience(candidateId, entries.map(normalizeExperience));
  }

  private async require(candidateId: string): Promise<void> {
    await this.candidateService.ensureAggregate(candidateId);
    if (!this.candidateService.find(candidateId)) {
      throw new TranslatableError('candidate.profile.validation.notFound');
    }
  }
}
