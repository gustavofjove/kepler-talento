import type { ApiTransport } from '../../../core/http/api-transport';
import { signal, type WritableSignal } from '../../../core/state/signal';
import {
  ALL_CANDIDATE_STATUSES,
  CriteriaFilter,
  EMPTY_SEARCH_FILTERS,
  SearchFilters,
  SearchPreset,
} from '../models/search.models';

/**
 * The last filter set the user ran, remembered per browser.
 *
 * This is the one thing about search that is still local, and deliberately so: it is a
 * convenience for returning to the same screen on the same machine, not shared or
 * authoritative state. Saved searches themselves live in PostgreSQL under their owner.
 *
 * Saved searches themselves used to live under a browser key of their own. That key is gone
 * from this source - not merely unused but unwritten, so it cannot be resurrected by a
 * careless call - along with everything that read it. Browser data has no trustworthy owner
 * identity, and attaching it to whichever API actor next opened a shared machine could
 * disclose someone's search terms or hand their saved searches to a colleague. Retained
 * browser values are ignored: never read, never uploaded, never deleted.
 */
const STORAGE_LAST_FILTERS_KEY = 'rrhh.search.last-filters.v1';

/** Shape of filters persisted before skill/level criteria replaced the plain value arrays. */
interface LegacyFilters {
  languageValues?: unknown;
  programValues?: unknown;
}

export type PresetsStatus = 'idle' | 'loading' | 'loaded' | 'failed';

export interface PresetsState {
  status: PresetsStatus;
  presets: SearchPreset[];
}

const INITIAL_STATE: PresetsState = { status: 'idle', presets: [] };

export class SearchPresetsService {
  /**
   * Presets are read during render, so the service holds a signal and components subscribe
   * to it through `useSearchPresets()` rather than reading it inside `useServices()`.
   */
  readonly state: WritableSignal<PresetsState> = signal<PresetsState>(INITIAL_STATE);

  constructor(private readonly transport: ApiTransport) {}

  /**
   * Loads the current actor's saved searches.
   *
   * A failure leaves the previously loaded list in place and reports `failed`: dropping the
   * presets on a transient network error would look to the user like their saved searches
   * had been deleted.
   */
  async load(): Promise<SearchPreset[]> {
    this.state.update((current) => ({ ...current, status: 'loading' }));
    try {
      const presets = await this.transport.request<SearchPreset[]>('/search-presets');
      const normalized = presets.map((preset) => this.normalizePreset(preset));
      this.state.set({ status: 'loaded', presets: normalized });
      return normalized;
    } catch (error) {
      this.state.update((current) => ({ ...current, status: 'failed' }));
      throw error;
    }
  }

  listPresets(): SearchPreset[] {
    return this.state().presets;
  }

  /** Creates a saved search. The server rejects a name this owner already uses. */
  async createPreset(name: string, filters: SearchFilters): Promise<SearchPreset> {
    const created = await this.transport.request<SearchPreset>('/search-presets', {
      method: 'POST',
      body: JSON.stringify({ name, filters }),
    });
    await this.load();
    return this.normalizePreset(created);
  }

  /** Renames a saved search and replaces its filters; both travel in one write. */
  async updatePreset(id: string, name: string, filters: SearchFilters): Promise<SearchPreset> {
    const updated = await this.transport.request<SearchPreset>(
      `/search-presets/${encodeURIComponent(id)}`,
      { method: 'PUT', body: JSON.stringify({ name, filters }) },
    );
    await this.load();
    return this.normalizePreset(updated);
  }

  /**
   * Applies a saved search: the server answers with its filters and records the use, so the
   * "last used" ordering reflects real use rather than what this browser happens to know.
   */
  async applyPreset(id: string): Promise<SearchFilters> {
    const applied = await this.transport.request<SearchPreset>(
      `/search-presets/${encodeURIComponent(id)}/use`,
      { method: 'POST', body: JSON.stringify({}) },
    );
    await this.load();
    return this.normalizeFilters(applied.filters);
  }

  async removePreset(id: string): Promise<void> {
    await this.transport.request<void>(`/search-presets/${encodeURIComponent(id)}`, {
      method: 'DELETE',
    });
    await this.load();
  }

  rememberLastFilters(filters: SearchFilters): void {
    try {
      localStorage.setItem(
        STORAGE_LAST_FILTERS_KEY,
        JSON.stringify(this.normalizeFilters(filters)),
      );
    } catch {
      // A browser that refuses storage loses a convenience, not a feature.
    }
  }

  loadLastFilters(): SearchFilters {
    try {
      const raw = localStorage.getItem(STORAGE_LAST_FILTERS_KEY);
      if (!raw) {
        return this.emptyFilters();
      }
      return this.normalizeFilters(JSON.parse(raw));
    } catch {
      return this.emptyFilters();
    }
  }

  emptyFilters(): SearchFilters {
    return structuredClone(EMPTY_SEARCH_FILTERS);
  }

  private normalizePreset(preset: SearchPreset): SearchPreset {
    return { ...preset, filters: this.normalizeFilters(preset.filters) };
  }

  /**
   * Defensive normalization of a filter value from outside this module - a stored last-filter
   * value or an API response. The server validates what it stores, so this is not a second
   * authority; it is what keeps a hand-edited or half-written browser value from reaching
   * the form as `undefined`.
   */
  private normalizeFilters(input: Partial<SearchFilters> & LegacyFilters): SearchFilters {
    return {
      text: typeof input?.text === 'string' ? input.text : '',
      // No status selection and every status selected mean the same query, so an empty
      // stored value restores as the default: all statuses checked.
      statusValues:
        Array.isArray(input?.statusValues) && input.statusValues.length
          ? input.statusValues
          : [...ALL_CANDIDATE_STATUSES],
      skillCriteria: this.normalizeCriteria(input?.skillCriteria),
      skillMode: input?.skillMode === 'ALL' ? 'ALL' : 'ANY',
      languageCriteria: this.normalizeCriteria(input?.languageCriteria, input?.languageValues),
      languageMode: input?.languageMode === 'ALL' ? 'ALL' : 'ANY',
      programCriteria: this.normalizeCriteria(input?.programCriteria, input?.programValues),
      programMode: input?.programMode === 'ALL' ? 'ALL' : 'ANY',
      hasCv: input?.hasCv === 'yes' || input?.hasCv === 'no' ? input.hasCv : '',
    };
  }

  /**
   * Accepts the current shape and the older last-filter shape that stored plain value
   * arrays. This conversion applies to the local last-filter value only; it is not a preset
   * migration, and nothing here can turn browser data into a saved search.
   */
  private normalizeCriteria(input: unknown, legacyValues?: unknown): CriteriaFilter[] {
    if (Array.isArray(input)) {
      return input
        .map((item) => ({
          value: typeof item?.value === 'string' ? item.value : '',
          level: typeof item?.level === 'string' ? item.level : '',
        }))
        .filter((item) => item.value.trim().length > 0);
    }
    if (Array.isArray(legacyValues)) {
      return legacyValues
        .filter((value): value is string => typeof value === 'string' && value.trim().length > 0)
        .map((value) => ({ value, level: '' }));
    }
    return [];
  }
}
