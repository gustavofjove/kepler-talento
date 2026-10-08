import { readFileSync } from 'node:fs';
import { resolve } from 'node:path';

import {
  CATALOG_COLORS,
  DEFAULT_CATALOG_COLOR,
  isColorableFamily,
} from '../../src/app/features/catalogs/catalog-color.logic';
import es from '../../src/assets/i18n/es.json';
import { repoRoot } from '../repo-root';

/**
 * KTL-41 stores colours as tokens the API validates. The frontend cannot import the C# list, so
 * this reads `CatalogColors.All` out of its source: a token added on one side only fails here
 * rather than as a 400 from the dialog or a chip silently drawn in the default.
 */
const PALETTE_SOURCE = resolve(repoRoot, 'backend/Domain/Catalogs/CatalogColors.cs');

function readApiPalette(): { all: string[]; defaultColor: string } {
  const source = readFileSync(PALETTE_SOURCE, 'utf8');
  const constants = new Map<string, string>();
  for (const match of source.matchAll(/public const string (\w+) = "([^"]+)";/g)) {
    constants.set(match[1], match[2]);
  }
  const all = /public static readonly IReadOnlyList<string> All =\s*\[([^\]]*)\]/.exec(source);
  const defaultName = /public const string Default = (\w+);/.exec(source);
  if (!all || !defaultName) {
    throw new Error('CatalogColors.All or CatalogColors.Default was not found');
  }
  const value = (name: string): string => {
    const resolved = constants.get(name);
    if (!resolved) throw new Error(`CatalogColors references unknown constant ${name}`);
    return resolved;
  };
  return {
    all: all[1]
      .split(',')
      .map((entry) => entry.trim())
      .filter((entry) => entry.length > 0)
      .map(value),
    defaultColor: value(defaultName[1]),
  };
}

describe('catalog colour palette', () => {
  it('matches the palette the API accepts, element for element', () => {
    const api = readApiPalette();
    expect([...CATALOG_COLORS]).toEqual(api.all);
    expect(DEFAULT_CATALOG_COLOR).toBe(api.defaultColor);
  });

  it('names every colour in Spanish', () => {
    const messages = es as Record<string, string>;
    for (const color of CATALOG_COLORS) {
      expect(messages[`catalogs.color.name.${color}`], color).toBeTruthy();
    }
  });

  it('colours only the chip families', () => {
    expect(isColorableFamily('skill')).toBe(true);
    expect(isColorableFamily('language')).toBe(true);
    expect(isColorableFamily('program')).toBe(true);
    expect(isColorableFamily('tag')).toBe(true);
    for (const family of [
      'language_level',
      'program_level',
      'skill_level',
      'education_type',
      'education_status',
      'sector',
    ] as const) {
      expect(isColorableFamily(family)).toBe(false);
    }
  });
});
