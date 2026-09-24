/** A catalog-backed multi-value family edited with the value picker. */
export type CatalogFamilyKind = 'skill' | 'language' | 'program' | 'tag';

/** The one order every host shows the families in (KTL-27). */
export const CATALOG_FAMILY_ORDER: readonly CatalogFamilyKind[] = [
  'skill',
  'language',
  'program',
  'tag',
];
