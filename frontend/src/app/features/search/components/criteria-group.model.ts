import type { CatalogFamily } from '../../catalogs/models/catalog.models';

export type CriteriaKind = 'skill' | 'language' | 'program' | 'tag';

export interface CriteriaGroupDefinition {
  kind: CriteriaKind;
  labelKey: string;
  levelLabelKey?: string;
  addLabelKey: string;
  valueFamily: CatalogFamily;
  levelFamily?: CatalogFamily;
}

export const CRITERIA_GROUPS: CriteriaGroupDefinition[] = [
  {
    kind: 'skill',
    labelKey: 'search.criteria.skill.label',
    levelLabelKey: 'search.criteria.skill.level',
    addLabelKey: 'search.criteria.skill.add',
    valueFamily: 'skill',
    levelFamily: 'skill_level',
  },
  {
    kind: 'language',
    labelKey: 'search.criteria.language.label',
    levelLabelKey: 'search.criteria.language.level',
    addLabelKey: 'search.criteria.language.add',
    valueFamily: 'language',
    levelFamily: 'language_level',
  },
  {
    kind: 'program',
    labelKey: 'search.criteria.program.label',
    levelLabelKey: 'search.criteria.program.level',
    addLabelKey: 'search.criteria.program.add',
    valueFamily: 'program',
    levelFamily: 'program_level',
  },
  {
    kind: 'tag',
    labelKey: 'search.criteria.tag.label',
    addLabelKey: 'search.criteria.tag.add',
    valueFamily: 'tag',
  },
];
