import {
  ALL_CANDIDATE_STATUSES,
  CriteriaFilter,
  EMPTY_SEARCH_FILTERS,
  SearchFilters,
  SearchPreset,
} from '../models/search.models';

const STORAGE_PRESETS_KEY = 'rrhh.search.presets.v1';
const STORAGE_LAST_FILTERS_KEY = 'rrhh.search.last-filters.v1';

/** Shape of filters persisted before skill/level criteria replaced the plain value arrays. */
interface LegacyFilters {
  languageValues?: unknown;
  programValues?: unknown;
}

export class SearchPresetsService {
  rememberLastFilters(filters: SearchFilters): void {
    localStorage.setItem(STORAGE_LAST_FILTERS_KEY, JSON.stringify(this.normalizeFilters(filters)));
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

  listPresets(): SearchPreset[] {
    return this.readPresets().sort((a, b) => a.name.localeCompare(b.name));
  }

  savePreset(name: string, filters: SearchFilters): SearchPreset {
    const cleanName = name.trim();
    if (!cleanName) {
      throw new Error('El nombre del preset es obligatorio.');
    }

    const now = new Date().toISOString();
    const presets = this.readPresets();
    const normalized = this.normalizeFilters(filters);
    const existing = presets.find((item) => item.name.toLowerCase() === cleanName.toLowerCase());

    if (existing) {
      const updated: SearchPreset = {
        ...existing,
        name: cleanName,
        filters: normalized,
        updatedAt: now,
      };
      this.writePresets(presets.map((item) => (item.id === updated.id ? updated : item)));
      return updated;
    }

    const created: SearchPreset = {
      id: this.generateId(),
      name: cleanName,
      filters: normalized,
      createdAt: now,
      updatedAt: now,
    };
    this.writePresets([...presets, created]);
    return created;
  }

  applyPreset(presetId: string): SearchFilters {
    const presets = this.readPresets();
    const preset = presets.find((item) => item.id === presetId);
    if (!preset) {
      throw new Error('Preset de búsqueda no encontrado.');
    }

    const now = new Date().toISOString();
    const updated = { ...preset, lastUsedAt: now, updatedAt: now };
    this.writePresets(presets.map((item) => (item.id === updated.id ? updated : item)));
    return this.normalizeFilters(updated.filters);
  }

  removePreset(presetId: string): void {
    const presets = this.readPresets();
    this.writePresets(presets.filter((item) => item.id !== presetId));
  }

  emptyFilters(): SearchFilters {
    return structuredClone(EMPTY_SEARCH_FILTERS);
  }

  private readPresets(): SearchPreset[] {
    try {
      const raw = localStorage.getItem(STORAGE_PRESETS_KEY);
      if (!raw) {
        return [];
      }
      const parsed = JSON.parse(raw) as SearchPreset[];
      if (!Array.isArray(parsed)) {
        return [];
      }
      return parsed.map((preset) => ({
        id: preset.id,
        name: preset.name,
        filters: this.normalizeFilters(preset.filters),
        createdAt: preset.createdAt,
        updatedAt: preset.updatedAt,
        lastUsedAt: preset.lastUsedAt,
      }));
    } catch {
      return [];
    }
  }

  private writePresets(presets: SearchPreset[]): void {
    localStorage.setItem(STORAGE_PRESETS_KEY, JSON.stringify(presets));
  }

  private normalizeFilters(input: Partial<SearchFilters> & LegacyFilters): SearchFilters {
    return {
      text: typeof input.text === 'string' ? input.text : '',
      // No status selection and every status selected mean the same query, so an empty
      // stored value restores as the default: all statuses checked.
      statusValues:
        Array.isArray(input.statusValues) && input.statusValues.length
          ? input.statusValues
          : [...ALL_CANDIDATE_STATUSES],
      skillCriteria: this.normalizeCriteria(input.skillCriteria),
      skillMode: input.skillMode === 'ALL' ? 'ALL' : 'ANY',
      languageCriteria: this.normalizeCriteria(input.languageCriteria, input.languageValues),
      languageMode: input.languageMode === 'ALL' ? 'ALL' : 'ANY',
      programCriteria: this.normalizeCriteria(input.programCriteria, input.programValues),
      programMode: input.programMode === 'ALL' ? 'ALL' : 'ANY',
      hasCv: input.hasCv === 'yes' || input.hasCv === 'no' ? input.hasCv : '',
    };
  }

  /** Accepts the current shape and migrates presets stored before levels existed. */
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

  private generateId(): string {
    return `sp_${Math.random().toString(36).slice(2, 10)}`;
  }
}
