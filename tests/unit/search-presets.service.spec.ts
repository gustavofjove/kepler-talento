import { SearchPresetsService } from '../../src/app/features/search/services/search-presets.service';
import { SearchFilters } from '../../src/app/features/search/models/search.models';

describe('SearchPresetsService', () => {
  let service: SearchPresetsService;

  beforeEach(() => {
    localStorage.clear();
    service = new SearchPresetsService();
  });

  it('saves, lists, applies and removes search presets', () => {
    const filters: SearchFilters = {
      ...service.emptyFilters(),
      text: 'ana',
      statusValues: ['available'],
      hasCv: 'yes',
    };

    const saved = service.savePreset('Disponibles con CV', filters);
    const listed = service.listPresets();

    expect(listed).toHaveLength(1);
    expect(listed[0].name).toBe('Disponibles con CV');

    const applied = service.applyPreset(saved.id);
    expect(applied.text).toBe('ana');
    expect(applied.statusValues).toEqual(['available']);
    expect(applied.hasCv).toBe('yes');

    service.removePreset(saved.id);
    expect(service.listPresets()).toHaveLength(0);
  });

  it('updates preset when saving another with the same name (case-insensitive)', () => {
    const first = service.savePreset('Mi preset', {
      ...service.emptyFilters(),
      text: 'uno',
    });

    const second = service.savePreset('mi PRESET', {
      ...service.emptyFilters(),
      text: 'dos',
    });

    expect(second.id).toBe(first.id);
    expect(service.listPresets()).toHaveLength(1);
    expect(service.listPresets()[0].filters.text).toBe('dos');
  });

  it('persists and restores last filters, falling back to empty on invalid payload', () => {
    const lastFilters = {
      ...service.emptyFilters(),
      text: 'restored',
      languageValues: ['Ingles'],
      programMode: 'ALL' as const,
    };

    service.rememberLastFilters(lastFilters);
    const restored = service.loadLastFilters();
    expect(restored.text).toBe('restored');
    expect(restored.languageValues).toEqual(['Ingles']);
    expect(restored.programMode).toBe('ALL');

    localStorage.setItem('rrhh.search.last-filters.v1', '{broken json');
    const fallback = service.loadLastFilters();
    expect(fallback).toEqual(service.emptyFilters());
  });
});
