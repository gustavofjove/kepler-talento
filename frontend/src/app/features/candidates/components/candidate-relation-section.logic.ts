import type { TFunction } from 'i18next';
import { TranslatableError } from '../../../core/i18n/translatable-error';
import type { PickerItem } from '../../catalogs/components/catalog-value-picker.logic';
import type { CatalogFamily } from '../../catalogs/models/catalog.models';
import type { Candidate } from '../models/candidate.models';
import type { CandidateRelationsService } from '../services/candidate-relations.service';

export type RelationKind = 'language' | 'skill' | 'program' | 'tag';

/** The detail field a relation family edits on its chip, besides the level. */
export type RelationDetail = 'certification' | 'yearsExperience';

/**
 * Everything that differs between the four catalog-backed relation sections. The section
 * component is one implementation driven by this table (design D5).
 */
export interface RelationDefinition {
  kind: RelationKind;
  /** Section wrapper test id, kept from the four components this replaced. */
  testId: string;
  /** Picker prefix: test ids and the input's `name`. */
  idPrefix: string;
  titleKey: string;
  emptyKey: string;
  addLabelKey: string;
  valueFamily: CatalogFamily;
  levelFamily?: CatalogFamily;
  detail?: RelationDetail;
  items: (candidate: Candidate) => PickerItem[];
  add: (
    service: CandidateRelationsService,
    candidate: Candidate,
    item: PickerItem,
  ) => Promise<void>;
  update: (
    service: CandidateRelationsService,
    candidate: Candidate,
    item: PickerItem,
  ) => Promise<void>;
  remove: (service: CandidateRelationsService, candidateId: string, key: string) => Promise<void>;
}

const text = (item: PickerItem, field: RelationDetail): string =>
  String(item.details?.[field] ?? '');

const years = (item: PickerItem): number | undefined => {
  const value = item.details?.['yearsExperience'];
  return value === undefined || value === '' ? undefined : Number(value);
};

const existing = <T extends { id: string }>(entries: T[], item: PickerItem): T => {
  const entry = entries.find((candidate) => candidate.id === item.key);
  if (!entry) throw new TranslatableError('candidate.profile.validation.entryNotFound');
  return entry;
};

export const RELATION_DEFINITIONS: Record<RelationKind, RelationDefinition> = {
  language: {
    kind: 'language',
    testId: 'candidate-languages',
    idPrefix: 'candidate-language',
    titleKey: 'candidate.profile.languages.title',
    emptyKey: 'candidate.profile.languages.empty',
    addLabelKey: 'catalogPicker.add.language',
    valueFamily: 'language',
    levelFamily: 'language_level',
    detail: 'certification',
    items: (candidate) =>
      candidate.languages.map((entry) => ({
        key: entry.id,
        value: entry.language,
        level: entry.level,
        details: { certification: entry.certification ?? '' },
      })),
    add: (service, candidate, item) =>
      service.addLanguage(candidate.id, {
        language: item.value,
        level: item.level,
        certification: text(item, 'certification'),
      }),
    update: (service, candidate, item) =>
      service.updateLanguage(candidate.id, {
        ...existing(candidate.languages, item),
        level: item.level,
        certification: text(item, 'certification'),
      }),
    remove: (service, candidateId, key) => service.removeLanguage(candidateId, key),
  },
  skill: {
    kind: 'skill',
    testId: 'candidate-skills',
    idPrefix: 'candidate-skill',
    titleKey: 'candidate.profile.skills.title',
    emptyKey: 'candidate.profile.skills.empty',
    addLabelKey: 'catalogPicker.add.skill',
    valueFamily: 'skill',
    levelFamily: 'skill_level',
    items: (candidate) =>
      candidate.skills.map((entry) => ({ key: entry.id, value: entry.skill, level: entry.level })),
    add: (service, candidate, item) =>
      service.addSkill(candidate.id, { skill: item.value, level: item.level }),
    update: (service, candidate, item) =>
      service.updateSkill(candidate.id, {
        ...existing(candidate.skills, item),
        level: item.level,
      }),
    remove: (service, candidateId, key) => service.removeSkill(candidateId, key),
  },
  program: {
    kind: 'program',
    testId: 'candidate-programs',
    idPrefix: 'candidate-program',
    titleKey: 'candidate.profile.programs.title',
    emptyKey: 'candidate.profile.programs.empty',
    addLabelKey: 'catalogPicker.add.program',
    valueFamily: 'program',
    levelFamily: 'program_level',
    detail: 'yearsExperience',
    items: (candidate) =>
      candidate.programs.map((entry) => ({
        key: entry.id,
        value: entry.program,
        level: entry.level,
        details: { yearsExperience: entry.yearsExperience },
      })),
    add: (service, candidate, item) =>
      service.addProgram(candidate.id, {
        program: item.value,
        level: item.level,
        yearsExperience: years(item),
      }),
    update: (service, candidate, item) =>
      service.updateProgram(candidate.id, {
        ...existing(candidate.programs, item),
        level: item.level,
        yearsExperience: years(item),
      }),
    remove: (service, candidateId, key) => service.removeProgram(candidateId, key),
  },
  tag: {
    kind: 'tag',
    testId: 'candidate-tags',
    idPrefix: 'candidate-tag',
    titleKey: 'candidate.profile.tags.title',
    emptyKey: 'candidate.profile.tags.empty',
    addLabelKey: 'catalogPicker.add.tag',
    valueFamily: 'tag',
    items: (candidate) =>
      candidate.tags.map((entry) => ({ key: entry.id, value: entry.tag, level: '' })),
    add: (service, candidate, item) => service.addTag(candidate.id, { tag: item.value }),
    // Tags have no level or details, so there is nothing to change in place.
    update: async () => undefined,
    remove: (service, candidateId, key) => service.removeTag(candidateId, key),
  },
};

/** Extra chip text for a relation's detail, e.g. «TOEFL» or «3 años». */
export function detailText(
  detail: RelationDetail | undefined,
  item: PickerItem,
  t: TFunction,
): string | undefined {
  if (detail === 'certification') return text(item, 'certification') || undefined;
  if (detail === 'yearsExperience') {
    const value = years(item);
    return value === undefined ? undefined : t('candidate.profile.yearsCount', { years: value });
  }
  return undefined;
}

export type RelationIntent =
  | { type: 'add'; item: PickerItem }
  | { type: 'change'; item: PickerItem }
  | { type: 'remove'; item: PickerItem };

export interface RelationWrite {
  state: 'pending' | 'error';
  error?: string;
  intent: RelationIntent;
}

/**
 * The chips a section shows: the saved entries with each one's write state laid over it,
 * plus adds not yet absorbed into the aggregate. A pending change shows its intended level;
 * a failed one shows what is actually saved.
 */
export function mergeWrites(
  saved: PickerItem[],
  writes: Record<string, RelationWrite>,
): PickerItem[] {
  const savedValues = new Set(saved.map((item) => item.value));
  const shown = saved.map((item) => {
    const write = writes[item.key];
    if (!write) return item;
    const base =
      write.state === 'pending' && write.intent.type === 'change' ? write.intent.item : item;
    return { ...base, status: write.state, error: write.error };
  });
  for (const [key, write] of Object.entries(writes)) {
    if (write.intent.type !== 'add' || saved.some((item) => item.key === key)) continue;
    // Absorbed by the aggregate under its new id: the saved entry already stands for it.
    if (write.state === 'pending' && savedValues.has(write.intent.item.value)) continue;
    shown.push({ ...write.intent.item, status: write.state, error: write.error });
  }
  return shown;
}
