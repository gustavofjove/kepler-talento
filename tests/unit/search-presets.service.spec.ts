import { readFileSync, readdirSync, statSync } from 'node:fs';
import { join } from 'node:path';
import { ApiTransport } from '../../src/app/core/http/api-transport';
import { SearchPresetsService } from '../../src/app/features/search/services/search-presets.service';
import type { SearchFilters } from '../../src/app/features/search/models/search.models';

const LEGACY_PRESETS_KEY = 'rrhh.search.presets.v1';
const LAST_FILTERS_KEY = 'rrhh.search.last-filters.v1';

describe('SearchPresetsService', () => {
  const preset = (overrides: Record<string, unknown> = {}) => ({
    id: 'p-1',
    name: 'Disponibles con CV',
    filters: {
      text: 'ana',
      statusValues: ['available'],
      skillCriteria: [],
      skillMode: 'ANY',
      languageCriteria: [],
      languageMode: 'ANY',
      programCriteria: [],
      programMode: 'ANY',
      hasCv: 'yes',
    },
    createdAt: '2026-03-01T09:00:00Z',
    updatedAt: '2026-03-01T09:00:00Z',
    ...overrides,
  });

  const json = (body: unknown, status = 200) =>
    new Response(status === 204 ? null : JSON.stringify(body), {
      status,
      headers: status === 204 ? {} : { 'content-type': 'application/json' },
    });

  const build = (fetcher: ReturnType<typeof vi.fn>) =>
    new SearchPresetsService(new ApiTransport('/api', 1_000, fetcher));

  const called = (fetcher: ReturnType<typeof vi.fn>) =>
    fetcher.mock.calls.map((call) => `${call[1]?.method ?? 'GET'} ${call[0]}`);

  beforeEach(() => {
    localStorage.clear();
  });

  it('starts idle and exposes loading then loaded state', async () => {
    const fetcher = vi.fn().mockResolvedValue(json([preset()]));
    const service = build(fetcher);
    expect(service.state()).toEqual({ status: 'idle', presets: [] });

    const loading = service.load();
    expect(service.state().status).toBe('loading');
    await loading;

    expect(service.state().status).toBe('loaded');
    expect(service.listPresets()).toHaveLength(1);
    expect(called(fetcher)).toEqual(['GET /api/search-presets']);
  });

  it('reports a failed load without discarding what it already had', async () => {
    const fetcher = vi
      .fn()
      .mockResolvedValueOnce(json([preset()]))
      .mockResolvedValueOnce(json({ status: 500 }, 500));
    const service = build(fetcher);
    await service.load();

    await expect(service.load()).rejects.toBeInstanceOf(Error);

    // Dropping the list on a transient error would look to the user exactly like their
    // saved searches having been deleted.
    expect(service.state().status).toBe('failed');
    expect(service.listPresets()).toHaveLength(1);
  });

  it('creates, renames, applies and deletes through the API and refreshes the list', async () => {
    const fetcher = vi.fn().mockImplementation((_input: string, init?: RequestInit) => {
      const method = init?.method ?? 'GET';
      if (method === 'DELETE') return Promise.resolve(json(null, 204));
      // Every mutation is followed by a refresh, so the list response must be the one a
      // plain GET gets back.
      if (method === 'GET') return Promise.resolve(json([preset()]));
      return Promise.resolve(json(preset()));
    });
    const service = build(fetcher);
    const filters: SearchFilters = { ...service.emptyFilters(), text: 'ana', hasCv: 'yes' };

    const created = await service.createPreset('Disponibles con CV', filters);
    expect(created.name).toBe('Disponibles con CV');

    await service.updatePreset('p-1', 'Renombrada', filters);
    const applied = await service.applyPreset('p-1');
    expect(applied.text).toBe('ana');
    expect(applied.hasCv).toBe('yes');

    await service.removePreset('p-1');

    expect(called(fetcher)).toEqual([
      'POST /api/search-presets',
      'GET /api/search-presets',
      'PUT /api/search-presets/p-1',
      'GET /api/search-presets',
      // Applying is a POST on a sub-resource because it records the use; a GET that wrote
      // would be a lie about the verb.
      'POST /api/search-presets/p-1/use',
      'GET /api/search-presets',
      'DELETE /api/search-presets/p-1',
      'GET /api/search-presets',
    ]);
  });

  it('surfaces a name conflict as a failure rather than swallowing it', async () => {
    const fetcher = vi
      .fn()
      .mockResolvedValue(
        json({ status: 409, code: 'search_preset.name.conflict', detail: 'Ya existe.' }, 409),
      );

    await expect(
      build(fetcher).createPreset('Duplicada', build(fetcher).emptyFilters()),
    ).rejects.toMatchObject({ code: 'CONFLICT' });
  });

  it('normalizes filters that arrive from the API', async () => {
    const fetcher = vi
      .fn()
      .mockResolvedValue(json([preset({ filters: { text: 'ana', hasCv: 'quizá' } })]));

    const [loaded] = await build(fetcher).load();

    // Defensive, not a second authority: the server validates what it stores, and this is
    // what keeps a surprising value from reaching the form as `undefined`.
    expect(loaded.filters.hasCv).toBe('');
    expect(loaded.filters.statusValues).toEqual([
      'new',
      'available',
      'in_process',
      'hired',
      'rejected',
    ]);
    expect(loaded.filters.skillMode).toBe('ANY');
  });

  it('keeps last filters in the browser and never uploads them', async () => {
    const fetcher = vi.fn().mockResolvedValue(json([]));
    const service = build(fetcher);

    service.rememberLastFilters({ ...service.emptyFilters(), text: 'ana', hasCv: 'yes' });
    const restored = service.loadLastFilters();

    expect(restored.text).toBe('ana');
    expect(restored.hasCv).toBe('yes');
    expect(localStorage.getItem(LAST_FILTERS_KEY)).toContain('ana');
    // Remembering a filter set is a local convenience and must never become a server write.
    expect(fetcher).not.toHaveBeenCalled();
  });

  it('falls back to empty filters when the stored last-filter value is unusable', () => {
    const service = build(vi.fn());

    localStorage.setItem(LAST_FILTERS_KEY, 'no json en absoluto');

    expect(service.loadLastFilters()).toEqual(service.emptyFilters());
  });

  it('still converts the older last-filter shape that stored plain value arrays', () => {
    const service = build(vi.fn());
    localStorage.setItem(
      LAST_FILTERS_KEY,
      JSON.stringify({ languageValues: ['Inglés'], programValues: ['Excel'] }),
    );

    const restored = service.loadLastFilters();

    expect(restored.languageCriteria).toEqual([{ value: 'Inglés', level: '' }]);
    expect(restored.programCriteria).toEqual([{ value: 'Excel', level: '' }]);
  });

  it('ignores legacy local presets entirely and never uploads or deletes them', async () => {
    const retained = JSON.stringify([
      { id: 'old-1', name: 'Antigua', filters: { text: 'marta' }, languageValues: ['Inglés'] },
    ]);
    localStorage.setItem(LEGACY_PRESETS_KEY, retained);
    const fetcher = vi.fn().mockResolvedValue(json([]));
    const service = build(fetcher);

    await service.load();

    expect(service.listPresets()).toEqual([]);
    expect(called(fetcher)).toEqual(['GET /api/search-presets']);
    // Left exactly as found: browser data has no trustworthy owner, so it is neither
    // claimed for the current actor nor destroyed on their behalf.
    expect(localStorage.getItem(LEGACY_PRESETS_KEY)).toBe(retained);
  });

  it('has no trace of the former preset storage key anywhere in the frontend source', () => {
    const offenders: string[] = [];
    const walk = (directory: string): void => {
      for (const entry of readdirSync(directory)) {
        const path = join(directory, entry);
        if (statSync(path).isDirectory()) {
          walk(path);
          continue;
        }
        if (!/\.(ts|tsx)$/.test(entry)) {
          continue;
        }
        if (readFileSync(path, 'utf8').includes(LEGACY_PRESETS_KEY)) {
          offenders.push(path);
        }
      }
    };
    walk(join(process.cwd(), 'src'));

    // The key is absent from source rather than merely unused: an unused constant is one
    // careless call away from writing browser presets again.
    expect(offenders).toEqual([]);
  });
});
