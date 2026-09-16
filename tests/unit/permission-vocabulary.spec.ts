import { readFileSync } from 'node:fs';
import { resolve } from 'node:path';

import { ALL_PERMISSIONS } from '../../src/app/shared/models/auth.models';

/**
 * KTL-16 chose one permission vocabulary over a mapping layer (design D10), which only holds
 * if the two ends cannot drift. The frontend cannot import the C# catalogue, so this reads
 * `Permissions.All` out of the source that defines it. A permission added on either side
 * without the other fails here rather than at runtime as a silently ungated control.
 */
const CATALOGUE_SOURCE = resolve(
  process.cwd(),
  'backend/Application/Abstractions/Identity/ICurrentActor.cs',
);

function readApiCatalogue(): string[] {
  const source = readFileSync(CATALOGUE_SOURCE, 'utf8');

  // The constants the `All` array is built from, as name -> value.
  const constants = new Map<string, string>();
  for (const match of source.matchAll(/public const string (\w+) = "([^"]+)";/g)) {
    constants.set(match[1], match[2]);
  }

  const all = /public static readonly string\[\] All =\s*\[([^\]]*)\]/.exec(source);
  if (!all) {
    throw new Error('Permissions.All was not found in ICurrentActor.cs');
  }

  return all[1]
    .split(',')
    .map((entry) => entry.trim())
    .filter((entry) => entry.length > 0)
    .map((name) => {
      const value = constants.get(name);
      if (!value) {
        throw new Error(`Permissions.All references unknown constant ${name}`);
      }
      return value;
    });
}

describe('permission vocabulary', () => {
  it('matches the catalogue the API exposes, element for element', () => {
    expect(ALL_PERMISSIONS).toEqual(readApiCatalogue());
  });

  it('uses the <resource>.<action> form throughout', () => {
    for (const permission of ALL_PERMISSIONS) {
      expect(permission).toMatch(/^[a-z]+\.[a-z_]+$/);
    }
  });

  it('holds no duplicates', () => {
    expect(new Set(ALL_PERMISSIONS).size).toBe(ALL_PERMISSIONS.length);
  });
});
