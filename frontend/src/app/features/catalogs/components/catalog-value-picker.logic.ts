/**
 * One catalog value held by a picker. The host maps its own model (a search criterion or a
 * candidate relation entry) to this shape and back; the picker never owns the list.
 */
export interface PickerItem {
  /** Stable identity: the relation id, or the normalized value for search criteria. */
  key: string;
  value: string;
  /** '' means any level, and is only meaningful for optional-level pickers. */
  level: string;
  details?: Record<string, string | number | undefined>;
  status?: 'pending' | 'error';
  error?: string;
}

/** Level families longer than this are offered through a select instead of a toggle group. */
export const MAX_TOGGLE_LEVELS = 6;

/** Case- and accent-insensitive form of a catalog name, used for matching only. */
export function normalizeName(value: string): string {
  return value.normalize('NFD').replace(/[̀-ͯ]/g, '').trim().toLowerCase();
}

/**
 * The values a picker offers for `query`: those whose name contains it, ignoring case and
 * accents, minus the values already held, in the catalog order `options` arrives in.
 */
export function filterOptions(
  options: readonly string[],
  query: string,
  items: readonly Pick<PickerItem, 'value'>[],
): string[] {
  const held = new Set(items.map((item) => normalizeName(item.value)));
  const needle = normalizeName(query);
  return options.filter((option) => {
    const name = normalizeName(option);
    return !held.has(name) && name.includes(needle);
  });
}

/** Test identifiers derived from a picker's prefix, identical on every host (design D3). */
export const pickerTestIds = (idPrefix: string) => ({
  root: `${idPrefix}-picker`,
  add: `${idPrefix}-add`,
  input: `${idPrefix}-input`,
  chip: `${idPrefix}-chip`,
  remove: `${idPrefix}-remove`,
  editor: `${idPrefix}-editor`,
  mode: `${idPrefix}-mode`,
  retry: `${idPrefix}-retry`,
});
