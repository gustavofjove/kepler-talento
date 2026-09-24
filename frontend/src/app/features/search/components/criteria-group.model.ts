import type { CatalogFamily } from '../../catalogs/models/catalog.models';

export type CriteriaKind = 'skill' | 'language' | 'program' | 'tag';

export interface CriteriaGroupDefinition {
  kind: CriteriaKind;
  labelKey: string;
  /** The picker input's accessible name and placeholder. */
  addLabelKey: string;
  valueFamily: CatalogFamily;
  levelFamily?: CatalogFamily;
}

export const CRITERIA_GROUPS: CriteriaGroupDefinition[] = [
  {
    kind: 'skill',
    labelKey: 'search.criteria.skill.label',
    addLabelKey: 'catalogPicker.add.skill',
    valueFamily: 'skill',
    levelFamily: 'skill_level',
  },
  {
    kind: 'language',
    labelKey: 'search.criteria.language.label',
    addLabelKey: 'catalogPicker.add.language',
    valueFamily: 'language',
    levelFamily: 'language_level',
  },
  {
    kind: 'program',
    labelKey: 'search.criteria.program.label',
    addLabelKey: 'catalogPicker.add.program',
    valueFamily: 'program',
    levelFamily: 'program_level',
  },
  {
    kind: 'tag',
    labelKey: 'search.criteria.tag.label',
    addLabelKey: 'catalogPicker.add.tag',
    valueFamily: 'tag',
  },
];
