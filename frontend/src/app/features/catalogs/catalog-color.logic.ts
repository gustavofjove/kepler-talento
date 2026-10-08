import type { CatalogColor, CatalogFamily } from './models/catalog.models';

/**
 * The palette in the order the colour dialog offers it (KTL-41). It mirrors `CatalogColors.All`
 * in the API's domain, which a unit spec checks.
 */
export const CATALOG_COLORS: readonly CatalogColor[] = [
  'orange',
  'yellow',
  'green',
  'teal',
  'blue',
  'indigo',
  'violet',
  'pink',
  'grey',
];

export const DEFAULT_CATALOG_COLOR: CatalogColor = 'orange';

/** The families drawn as chips, the only ones whose values may take a colour. */
const COLORABLE_FAMILIES: readonly CatalogFamily[] = ['skill', 'language', 'program', 'tag'];

export function isColorableFamily(family: CatalogFamily): boolean {
  return COLORABLE_FAMILIES.includes(family);
}

/** The `es.json` key of a colour's Spanish name, e.g. «Azul». */
export function colorNameKey(color: CatalogColor): string {
  return `catalogs.color.name.${color}`;
}
