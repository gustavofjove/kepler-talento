import { familiesWithoutLevels } from '../../src/app/features/candidates/components/candidate-competencies.logic';
import type { CatalogFamily } from '../../src/app/features/catalogs/models/catalog.models';

describe('familiesWithoutLevels', () => {
  const kinds = ['skill', 'language', 'program', 'tag'] as const;

  it('names every leveled family whose level catalog has no active value, in order', () => {
    const active: Partial<Record<CatalogFamily, string[]>> = {
      skill_level: [],
      language_level: ['A1'],
      program_level: [],
    };

    expect(familiesWithoutLevels(kinds, (family) => active[family] ?? [])).toEqual([
      'skill',
      'program',
    ]);
  });

  it('never names tags, which have no level', () => {
    expect(familiesWithoutLevels(kinds, () => [])).not.toContain('tag');
  });

  it('names nothing when every level family has an active value', () => {
    expect(familiesWithoutLevels(kinds, () => ['Bajo'])).toEqual([]);
  });
});
