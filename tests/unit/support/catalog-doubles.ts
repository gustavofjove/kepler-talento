import type {
  CatalogGateway,
  CatalogItemPayload,
} from '../../../src/app/features/catalogs/services/catalog.api';
import { CatalogService } from '../../../src/app/features/catalogs/services/catalog.service';
import {
  DEFAULT_CATALOGS,
  type CatalogFamily,
  type CatalogItem,
} from '../../../src/app/features/catalogs/models/catalog.models';

/**
 * In-memory stand-in for the catalog API, so component and service tests exercise the
 * real `CatalogService` without any network access.
 */
export class FakeCatalogApi implements CatalogGateway {
  readonly families = new Map<CatalogFamily, CatalogItem[]>();
  /** When set, every call rejects with it - used to drive the failure branch. */
  failure: Error | null = null;
  listCalls: CatalogFamily[] = [];

  constructor(seed: Partial<Record<CatalogFamily, string[]>> = DEFAULT_CATALOGS) {
    for (const [family, names] of Object.entries(seed) as [CatalogFamily, string[]][]) {
      this.families.set(
        family,
        names.map((nameEs, index) => ({
          id: `${family}-${index + 1}`,
          code: slug(nameEs),
          nameEs,
          sortOrder: index + 1,
          isActive: true,
          version: 1,
        })),
      );
    }
  }

  async list(family: CatalogFamily, _includeInactive: boolean): Promise<CatalogItem[]> {
    this.reject();
    this.listCalls.push(family);
    return (this.families.get(family) ?? []).map((item) => ({ ...item }));
  }

  async create(family: CatalogFamily, payload: CatalogItemPayload): Promise<CatalogItem> {
    this.reject();
    const items = this.families.get(family) ?? [];
    const created: CatalogItem = {
      id: `${family}-${items.length + 1}-new`,
      code: payload.code?.toUpperCase() || slug(payload.nameEs),
      nameEs: payload.nameEs,
      nameEn: payload.nameEn,
      sortOrder: items.length + 1,
      isActive: true,
      version: 1,
    };
    this.families.set(family, [...items, created]);
    return { ...created };
  }

  async update(
    family: CatalogFamily,
    id: string,
    payload: CatalogItemPayload & { version: number },
  ): Promise<CatalogItem> {
    this.reject();
    return this.patch(family, id, (item) => ({
      ...item,
      nameEs: payload.nameEs,
      code: payload.code?.toUpperCase() || item.code,
      nameEn: payload.nameEn,
      version: item.version + 1,
    }));
  }

  async reorder(family: CatalogFamily, orderedIds: string[]): Promise<CatalogItem[]> {
    this.reject();
    const items = this.families.get(family) ?? [];
    const reordered = orderedIds.map((id, index) => ({
      ...items.find((item) => item.id === id)!,
      sortOrder: index + 1,
    }));
    this.families.set(family, reordered);
    return reordered.map((item) => ({ ...item }));
  }

  async setActive(
    family: CatalogFamily,
    id: string,
    isActive: boolean,
    _version: number,
  ): Promise<CatalogItem> {
    this.reject();
    return this.patch(family, id, (item) => ({ ...item, isActive, version: item.version + 1 }));
  }

  private patch(
    family: CatalogFamily,
    id: string,
    change: (item: CatalogItem) => CatalogItem,
  ): CatalogItem {
    const items = this.families.get(family) ?? [];
    const next = items.map((item) => (item.id === id ? change(item) : item));
    this.families.set(family, next);
    return { ...next.find((item) => item.id === id)! };
  }

  private reject(): void {
    if (this.failure) {
      throw this.failure;
    }
  }
}

function slug(value: string): string {
  return value
    .normalize('NFD')
    .replace(/[̀-ͯ]/g, '')
    .replace(/[^a-zA-Z0-9]+/g, '_')
    .replace(/^_+|_+$/g, '')
    .toUpperCase();
}

export interface CatalogTestBed {
  service: CatalogService;
  api: FakeCatalogApi;
}

export function createCatalogTestBed(api = new FakeCatalogApi()): CatalogTestBed {
  return { service: new CatalogService(api), api };
}

/** A catalog service whose vocabulary has already loaded, for component tests. */
export async function loadedCatalogService(): Promise<CatalogService> {
  const { service } = createCatalogTestBed();
  await service.ensureLoaded();
  return service;
}
