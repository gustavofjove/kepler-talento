import { AppError } from '../../src/app/shared/models/error.models';
import type { CatalogService } from '../../src/app/features/catalogs/services/catalog.service';
import { createCatalogTestBed, FakeCatalogApi } from './support/catalog-doubles';

describe('CatalogService', () => {
  let service: CatalogService;
  let api: FakeCatalogApi;

  beforeEach(async () => {
    localStorage.clear();
    ({ service, api } = createCatalogTestBed());
    await service.ensureLoaded();
  });

  describe('loading', () => {
    it('starts idle and exposes the loaded vocabulary from the API', async () => {
      const fresh = createCatalogTestBed();
      expect(fresh.service.status).toBe('idle');
      expect(fresh.service.list('language')).toEqual([]);

      await fresh.service.ensureLoaded();

      expect(fresh.service.status).toBe('loaded');
      expect(fresh.service.activeNames('language')).toContain('Inglés');
    });

    it('loads only once for concurrent callers', async () => {
      const fresh = createCatalogTestBed();

      await Promise.all([fresh.service.ensureLoaded(), fresh.service.ensureLoaded()]);

      expect(fresh.api.listCalls.filter((family) => family === 'language')).toHaveLength(1);
    });

    it('surfaces a failed load as an error state with the Spanish message and no fallback data', async () => {
      const failing = new FakeCatalogApi();
      failing.failure = new AppError('INTERNAL_ERROR', 'No se ha podido conectar con el servidor.');
      const fresh = createCatalogTestBed(failing);

      await fresh.service.ensureLoaded();

      expect(fresh.service.status).toBe('error');
      expect(fresh.service.error?.message).toBe('No se ha podido conectar con el servidor.');
      // No local default-seed fallback: an outage must be visible, not papered over.
      expect(fresh.service.list('language', true)).toEqual([]);
      expect(fresh.service.activeNames('language')).toEqual([]);
    });

    it('does not read or write the legacy browser storage key', async () => {
      const fresh = createCatalogTestBed();
      await fresh.service.ensureLoaded();
      await fresh.service.create('language', 'Neerlandés');

      expect(localStorage.getItem('rrhh-catalogs')).toBeNull();
    });
  });

  describe('writes', () => {
    it('creates a catalog item and exposes it in active names', async () => {
      await service.create('language', 'Neerlandés');

      expect(service.activeNames('language')).toContain('Neerlandés');
    });

    it('rejects a blank name with the Spanish message before calling the API', async () => {
      const before = api.listCalls.length;

      await expect(service.create('language', '   ')).rejects.toThrow('El nombre es obligatorio.');
      expect(api.listCalls).toHaveLength(before);
    });

    it('propagates a duplicate-name problem from the API', async () => {
      api.failure = new AppError(
        'VALIDATION_ERROR',
        'Ya existe un valor con ese nombre.',
        undefined,
        undefined,
        'catalog.name.duplicate',
      );

      await expect(service.create('language', 'Inglés')).rejects.toThrow(
        'Ya existe un valor con ese nombre.',
      );
    });

    it('updates a catalog item name and code and refetches the family', async () => {
      const created = await service.create('skill', 'Negociación');
      const callsBefore = api.listCalls.filter((family) => family === 'skill').length;

      const updated = await service.update('skill', created.id, {
        nameEs: 'Negociación avanzada',
        code: 'NEG_AVZ',
      });

      expect(updated.nameEs).toBe('Negociación avanzada');
      expect(updated.code).toBe('NEG_AVZ');
      expect(service.activeNames('skill')).toContain('Negociación avanzada');
      expect(api.listCalls.filter((family) => family === 'skill').length).toBeGreaterThan(
        callsBefore,
      );
    });

    it('sends the version it read so a stale write can be rejected', async () => {
      const update = vi.spyOn(api, 'update');
      const target = service.list('skill', true)[0];

      await service.update('skill', target.id, { nameEs: 'Otro nombre' });

      expect(update).toHaveBeenCalledWith(
        'skill',
        target.id,
        expect.objectContaining({ version: target.version }),
      );
    });

    it('deactivates logically and hides the value from active names', async () => {
      const created = await service.create('program', 'Python');

      await service.toggleActive('program', created.id);

      expect(service.activeNames('program')).not.toContain('Python');
      // The value is retired, not removed.
      expect(service.list('program', true).some((item) => item.id === created.id)).toBe(true);
    });

    it('reactivates a deactivated value', async () => {
      const created = await service.create('program', 'Python');
      await service.toggleActive('program', created.id);

      await service.toggleActive('program', created.id);

      expect(service.activeNames('program')).toContain('Python');
    });

    it('moves an item by submitting the family complete new order', async () => {
      const reorder = vi.spyOn(api, 'reorder');
      await service.create('program_level', 'Experto');
      const second = await service.create('program_level', 'Senior');
      const initialIndex = service
        .list('program_level', true)
        .findIndex((item) => item.id === second.id);

      await service.move('program_level', second.id, -1);

      const items = service.list('program_level', true);
      expect(items.findIndex((item) => item.id === second.id)).toBe(initialIndex - 1);
      const [, orderedIds] = reorder.mock.calls[0];
      expect(orderedIds).toHaveLength(items.length);
      expect(new Set(orderedIds).size).toBe(items.length);
    });

    it('has no physical delete operation', () => {
      expect('remove' in service).toBe(false);
    });
  });

  describe('deactivation', () => {
    // The transitional screen-level refusal is gone with KTL-8. Deactivating a value
    // candidates reference is permitted: it stops being offered for new selections while
    // the records that already reference it keep resolving it. Deactivation is not
    // deletion, which is what protects the data.
    it('deactivates a value regardless of whether candidates reference it', async () => {
      const referenced = service.list('skill', true).find((item) => item.nameEs === 'Análisis');
      expect(referenced).toBeTruthy();

      await expect(service.toggleActive('skill', referenced!.id)).resolves.toMatchObject({
        isActive: false,
      });
    });

    it('allows deactivating a value no candidate uses', async () => {
      const unused = service.list('skill', true).find((item) => item.nameEs === 'Compras');

      await expect(service.toggleActive('skill', unused!.id)).resolves.toMatchObject({
        isActive: false,
      });
    });
  });
});
