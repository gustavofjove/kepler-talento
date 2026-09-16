import type { SearchPreset } from '../../search/models/search.models';

// The preset library is small and loaded whole, so it pages in the browser. The candidate
// list no longer does (KTL-18), which is why these live here rather than beside it.
export function totalPages(itemCount: number, pageSize: number): number {
  return Math.max(Math.ceil(itemCount / pageSize), 1);
}

export function paginate<T>(items: T[], page: number, pageSize: number): T[] {
  const validPage = Math.min(Math.max(page, 1), totalPages(items.length, pageSize));
  const start = (validPage - 1) * pageSize;
  return items.slice(start, start + pageSize);
}

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
