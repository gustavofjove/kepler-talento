import type { CatalogFamilyKind } from '../../catalogs/components/catalog-family-rows.logic';
import type { CatalogFamily } from '../../catalogs/models/catalog.models';

export type CriteriaKind = CatalogFamilyKind;

export interface CriteriaGroupDefinition {
  kind: CriteriaKind;
  labelKey: string;
  /** The picker input's accessible name and placeholder. */
  addLabelKey: string;
  valueFamily: CatalogFamily;
  levelFamily?: CatalogFamily;
}

/** Keyed by family; the display order is `CATALOG_FAMILY_ORDER`, shared with candidates. */
export const CRITERIA_GROUPS: Record<CriteriaKind, CriteriaGroupDefinition> = {
  skill: {
    kind: 'skill',
    labelKey: 'search.criteria.skill.label',
    addLabelKey: 'catalogPicker.add.skill',
    valueFamily: 'skill',
    levelFamily: 'skill_level',
  },
  language: {
    kind: 'language',
    labelKey: 'search.criteria.language.label',
    addLabelKey: 'catalogPicker.add.language',
    valueFamily: 'language',
    levelFamily: 'language_level',
  },
  program: {
    kind: 'program',
    labelKey: 'search.criteria.program.label',
    addLabelKey: 'catalogPicker.add.program',
    valueFamily: 'program',
    levelFamily: 'program_level',
  },
  tag: {
    kind: 'tag',
    labelKey: 'search.criteria.tag.label',
    addLabelKey: 'catalogPicker.add.tag',
    valueFamily: 'tag',
  },
};
