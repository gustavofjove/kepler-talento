import type { PickerItem } from '../../catalogs/components/catalog-value-picker.logic';
import type { CatalogFamily } from '../../catalogs/models/catalog.models';
import type { Candidate } from '../models/candidate.models';
import {
  itemsChanged,
  RELATION_DEFINITIONS,
  type RelationKind,
} from './candidate-relation-section.logic';

/** The Competencias draft: one list of picker items per family. */
export type FamilyItems = Record<RelationKind, PickerItem[]>;

/**
 * The families whose level is required but whose level catalog has no active value, in the
 * order given. They cannot be added to, since nothing is ever saved without a level.
 */
export function familiesWithoutLevels(
  kinds: readonly RelationKind[],
  activeNames: (family: CatalogFamily) => string[],
): RelationKind[] {
  return kinds.filter((kind) => {
    const { levelFamily } = RELATION_DEFINITIONS[kind];
    return levelFamily !== undefined && activeNames(levelFamily).length === 0;
  });
}

/** Every family's saved entries, as the picker shows them. */
export function savedFamilies(candidate: Candidate): FamilyItems {
  return {
    language: RELATION_DEFINITIONS.language.items(candidate),
    skill: RELATION_DEFINITIONS.skill.items(candidate),
    program: RELATION_DEFINITIONS.program.items(candidate),
    tag: RELATION_DEFINITIONS.tag.items(candidate),
  };
}

/** The families, in `order`, whose draft differs from what is saved: the ones «Guardar» writes. */
export function changedFamilies(
  order: readonly RelationKind[],
  saved: FamilyItems,
  draft: FamilyItems,
): RelationKind[] {
  return order.filter((kind) => itemsChanged(saved[kind], draft[kind]));
}
