import type { CatalogFamily } from '../../catalogs/models/catalog.models';
import { RELATION_DEFINITIONS, type RelationKind } from './candidate-relation-section.logic';

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
