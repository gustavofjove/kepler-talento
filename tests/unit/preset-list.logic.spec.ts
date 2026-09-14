import {
  filterPresets,
  nextPresetSort,
  sortPresets,
} from '../../src/app/features/admin/presets/preset-list.logic';
import {
  EMPTY_SEARCH_FILTERS,
  type SearchPreset,
} from '../../src/app/features/search/models/search.models';

const preset = (name: string, overrides: Partial<SearchPreset> = {}): SearchPreset => ({
  id: name,
  name,
  filters: structuredClone(EMPTY_SEARCH_FILTERS),
  createdAt: '2026-03-01T09:00:00Z',
  updatedAt: '2026-03-01T09:00:00Z',
  version: 1,
  ...overrides,
});

describe('preset list logic', () => {
  it('filters by name ignoring case and accents', () => {
    const presets = [preset('Inglés B2'), preset('Java senior')];

    expect(filterPresets(presets, 'ingles').map((item) => item.name)).toEqual(['Inglés B2']);
    expect(filterPresets(presets, '  ').map((item) => item.name)).toHaveLength(2);
  });

  it('sorts by name alphabetically without regard to accents', () => {
    const sorted = sortPresets([preset('Zeta'), preset('Ábaco'), preset('beta')], {
      field: 'name',
      direction: 'asc',
    });

    expect(sorted.map((item) => item.name)).toEqual(['Ábaco', 'beta', 'Zeta']);
  });

  it('puts never-used presets last when sorting by most recent use', () => {
    const sorted = sortPresets(
      [
        preset('Nunca'),
        preset('Antiguo', { lastUsedAt: '2026-03-02T09:00:00Z' }),
        preset('Reciente', { lastUsedAt: '2026-03-05T09:00:00Z' }),
      ],
      { field: 'lastUsedAt', direction: 'desc' },
    );

    expect(sorted.map((item) => item.name)).toEqual(['Reciente', 'Antiguo', 'Nunca']);
  });

  it('toggles the direction of the current column and starts dates newest first', () => {
    expect(nextPresetSort({ field: 'name', direction: 'asc' }, 'name')).toEqual({
      field: 'name',
      direction: 'desc',
    });
    expect(nextPresetSort({ field: 'name', direction: 'asc' }, 'updatedAt')).toEqual({
      field: 'updatedAt',
      direction: 'desc',
    });
  });
});
