import type { ApiTransport } from '../../../core/http/api-transport';
import { TranslatableError } from '../../../core/i18n/translatable-error';
import { signal, type WritableSignal } from '../../../core/state/signal';
import {
  ALL_CANDIDATE_STATUSES,
  CriteriaFilter,
  EMPTY_SEARCH_FILTERS,
  SearchFilters,
  SearchPreset,
} from '../models/search.models';

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
   * Loads the shared library.
   *
   * A failure leaves the previously loaded list in place and reports `failed`: dropping the
   * presets on a transient network error would look to the user like they had been deleted.
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

  /** Reads one preset as the server holds it now, including the version to write against. */
  async get(id: string): Promise<SearchPreset> {
    const preset = await this.transport.request<SearchPreset>(
      `/search-presets/${encodeURIComponent(id)}`,
    );
    return this.normalizePreset(preset);
  }

  /** Creates a saved search. The server rejects a name the library already uses. */
  async createPreset(name: string, filters: SearchFilters): Promise<SearchPreset> {
    const created = await this.transport.request<SearchPreset>('/search-presets', {
      method: 'POST',
      body: JSON.stringify({ name: this.requireName(name), filters }),
    });
    await this.load();
    return this.normalizePreset(created);
  }

  /**
   * Renames a saved search and replaces its filters; both travel in one write, against the
   * version the caller loaded, so someone else's newer change is a conflict and not overwritten.
   */
  async updatePreset(
    id: string,
    name: string,
    filters: SearchFilters,
    version: number,
  ): Promise<SearchPreset> {
    const updated = await this.transport.request<SearchPreset>(
      `/search-presets/${encodeURIComponent(id)}`,
      { method: 'PUT', body: JSON.stringify({ name: this.requireName(name), filters, version }) },
    );
    await this.load();
    return this.normalizePreset(updated);
  }

  /**
   * Applies a saved search: the server answers with its filters and records the use, so the
   * "last used" column reflects real use rather than what this browser happens to know.
   */
  async applyPreset(id: string): Promise<SearchFilters> {
    const applied = await this.transport.request<SearchPreset>(
      `/search-presets/${encodeURIComponent(id)}/use`,
      { method: 'POST', body: JSON.stringify({}) },
    );
    await this.load();
    return this.normalizeFilters(applied.filters);
  }

  async removePreset(id: string, version: number): Promise<void> {
    await this.transport.request<void>(
      `/search-presets/${encodeURIComponent(id)}?version=${encodeURIComponent(String(version))}`,
      { method: 'DELETE' },
    );
    await this.load();
  }

  emptyFilters(): SearchFilters {
    return structuredClone(EMPTY_SEARCH_FILTERS);
  }

  /**
   * Refuses a blank name before a request is made. A convenience for an immediate message,
   * not a second authority: the server validates every write it stores.
   */
  private requireName(name: string): string {
    const trimmed = name.trim();
    if (!trimmed) {
      throw new TranslatableError('presets.errors.nameRequired');
    }
    return trimmed;
  }

  private normalizePreset(preset: SearchPreset): SearchPreset {
    return { ...preset, filters: this.normalizeFilters(preset.filters) };
  }

  /**
   * Defensive normalization of a filter value from an API response. The server validates
   * what it stores, so this is not a second authority; it keeps an unexpected value from
   * reaching the form as `undefined`.
   */
  private normalizeFilters(input: Partial<SearchFilters>): SearchFilters {
    return {
      text: typeof input?.text === 'string' ? input.text : '',
      // No status selection and every status selected mean the same query.
      statusValues:
        Array.isArray(input?.statusValues) && input.statusValues.length
          ? input.statusValues
          : [...ALL_CANDIDATE_STATUSES],
      skillCriteria: this.normalizeCriteria(input?.skillCriteria),
      skillMode: input?.skillMode === 'ALL' ? 'ALL' : 'ANY',
      languageCriteria: this.normalizeCriteria(input?.languageCriteria),
      languageMode: input?.languageMode === 'ALL' ? 'ALL' : 'ANY',
      programCriteria: this.normalizeCriteria(input?.programCriteria),
      programMode: input?.programMode === 'ALL' ? 'ALL' : 'ANY',
      tagCriteria: this.normalizeCriteria(input?.tagCriteria),
      tagMode: input?.tagMode === 'ALL' ? 'ALL' : 'ANY',
      hasCv: input?.hasCv === 'yes' || input?.hasCv === 'no' ? input.hasCv : '',
    };
  }

  /**
   * Accepts only the current API shape of criteria.
   */
  private normalizeCriteria(input: unknown): CriteriaFilter[] {
    if (Array.isArray(input)) {
      return input
        .map((item) => ({
          value: typeof item?.value === 'string' ? item.value : '',
          level: typeof item?.level === 'string' ? item.level : '',
        }))
        .filter((item) => item.value.trim().length > 0);
    }
    return [];
  }
}
