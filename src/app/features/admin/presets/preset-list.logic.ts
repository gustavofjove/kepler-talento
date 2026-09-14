import type { SearchPreset } from '../../search/models/search.models';

export { paginate, totalPages } from '../../candidates/pages/candidate-list.logic';

export const PRESETS_ROUTE = '/app/admin/presets';

export type PresetSortField = 'name' | 'updatedAt' | 'lastUsedAt';
export type PresetSortDirection = 'asc' | 'desc';

export interface PresetSort {
  field: PresetSortField;
  direction: PresetSortDirection;
}

export const DEFAULT_PRESET_SORT: PresetSort = { field: 'name', direction: 'asc' };

/** Ignores case and accents, as the server does when it decides two names are the same. */
const fold = (value: string): string =>
  value.normalize('NFD').replace(/[̀-ͯ]/g, '').toLowerCase().trim();

export function filterPresets(presets: SearchPreset[], text: string): SearchPreset[] {
  const needle = fold(text);
  return needle ? presets.filter((preset) => fold(preset.name).includes(needle)) : presets;
}

export function sortPresets(presets: SearchPreset[], sort: PresetSort): SearchPreset[] {
  return presets.slice().sort((a, b) => {
    let value: number;
    if (sort.field === 'name') {
      value = fold(a.name).localeCompare(fold(b.name));
    } else if (sort.field === 'updatedAt') {
      value = a.updatedAt.localeCompare(b.updatedAt);
    } else {
      // Never used sorts as the oldest use, so it sinks to the end of a "most recent" order.
      value = (a.lastUsedAt ?? '').localeCompare(b.lastUsedAt ?? '');
    }
    return sort.direction === 'asc' ? value : -value;
  });
}

/** Same column toggles direction; a new column starts where it is most useful. */
export function nextPresetSort(current: PresetSort, field: PresetSortField): PresetSort {
  if (current.field === field) {
    return { field, direction: current.direction === 'asc' ? 'desc' : 'asc' };
  }
  return { field, direction: field === 'name' ? 'asc' : 'desc' };
}
