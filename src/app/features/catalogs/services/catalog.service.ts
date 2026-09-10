import { signal } from '../../../core/state/signal';
import { AppError, toAppError } from '../../../shared/models/error.models';
import {
  CatalogFamily,
  CatalogItem,
  CatalogLoadStatus,
  CATALOG_FAMILY_LABELS,
} from '../models/catalog.models';
import type { CatalogGateway } from './catalog.api';

const FAMILIES = Object.keys(CATALOG_FAMILY_LABELS) as CatalogFamily[];

export interface CatalogState {
  status: CatalogLoadStatus;
  /** Every family the API has returned, inactive values included. */
  items: Partial<Record<CatalogFamily, CatalogItem[]>>;
  error?: AppError;
}

/**
 * Catalog vocabulary, owned by the API.
 *
 * The reads stay synchronous so the seven consuming components keep their existing
 * shape, but they are now reads of a load state rather than of browser storage:
 * `status` tells a component whether an empty list means "still loading", "failed",
 * or "genuinely empty", and components are required to branch on it rather than
 * present a loading collection as a complete result.
 */
export class CatalogService {
  readonly catalogs = signal<CatalogState>({ status: 'idle', items: {} });

  private inFlight: Promise<void> | null = null;

  constructor(private readonly api: CatalogGateway) {}

  get status(): CatalogLoadStatus {
    return this.catalogs().status;
  }

  get error(): AppError | undefined {
    return this.catalogs().error;
  }

  /** Loads every family once. Repeated calls while a load is in flight share it. */
  ensureLoaded(): Promise<void> {
    const { status } = this.catalogs();
    if (status === 'loaded') {
      return Promise.resolve();
    }
    this.inFlight ??= this.loadAll().finally(() => {
      this.inFlight = null;
    });
    return this.inFlight;
  }

  async reload(): Promise<void> {
    this.inFlight = null;
    this.catalogs.set({ ...this.catalogs(), status: 'idle' });
    await this.ensureLoaded();
  }

  list(family: CatalogFamily, includeInactive = false): CatalogItem[] {
    const familyItems = this.catalogs().items[family] ?? [];
    const items = familyItems
      .slice()
      .sort((a, b) => a.sortOrder - b.sortOrder || a.nameEs.localeCompare(b.nameEs));
    return includeInactive ? items : items.filter((item) => item.isActive);
  }

  activeNames(family: CatalogFamily): string[] {
    return this.list(family).map((item) => item.nameEs);
  }

  async create(
    family: CatalogFamily,
    nameEs: string,
    code?: string,
    nameEn?: string,
  ): Promise<CatalogItem> {
    const trimmedName = nameEs.trim();
    if (!trimmedName) {
      throw new AppError('VALIDATION_ERROR', 'El nombre es obligatorio.');
    }
    const created = await this.api.create(family, {
      nameEs: trimmedName,
      code: code?.trim() || undefined,
      nameEn: nameEn?.trim() || undefined,
    });
    await this.refresh(family);
    return created;
  }

  async update(
    family: CatalogFamily,
    id: string,
    patch: { nameEs: string; code?: string; nameEn?: string },
  ): Promise<CatalogItem> {
    const trimmedName = patch.nameEs.trim();
    if (!trimmedName) {
      throw new AppError('VALIDATION_ERROR', 'El nombre es obligatorio.');
    }
    const current = this.find(family, id);
    const updated = await this.api.update(family, id, {
      nameEs: trimmedName,
      code: patch.code?.trim() || undefined,
      nameEn: patch.nameEn?.trim() || undefined,
      version: current.version,
    });
    await this.refresh(family);
    return updated;
  }

  /**
   * Deactivating a value candidates reference is permitted. The value stops being offered
   * for new selections while the records that already reference it keep resolving it —
   * deactivation is not deletion, which is the whole protection. The transitional
   * screen-level refusal this used to carry is gone with KTL-8, now that the API owns
   * candidate relations.
   */
  async toggleActive(family: CatalogFamily, id: string): Promise<CatalogItem> {
    const current = this.find(family, id);
    const updated = await this.api.setActive(family, id, !current.isActive, current.version);
    await this.refresh(family);
    return updated;
  }

  /** Moves a value within its family by submitting the family's complete new order. */
  async move(family: CatalogFamily, id: string, direction: -1 | 1): Promise<void> {
    const ordered = this.list(family, true);
    const index = ordered.findIndex((item) => item.id === id);
    if (index < 0) {
      return;
    }
    const target = index + direction;
    if (target < 0 || target >= ordered.length) {
      return;
    }
    const [moved] = ordered.splice(index, 1);
    ordered.splice(target, 0, moved);
    await this.api.reorder(
      family,
      ordered.map((item) => item.id),
    );
    await this.refresh(family);
  }

  private find(family: CatalogFamily, id: string): CatalogItem {
    const current = (this.catalogs().items[family] ?? []).find((item) => item.id === id);
    if (!current) {
      throw new AppError('NOT_FOUND', 'No se encontró el elemento del catálogo.');
    }
    return current;
  }

  private async loadAll(): Promise<void> {
    this.catalogs.set({ ...this.catalogs(), status: 'loading', error: undefined });
    try {
      const loaded = await Promise.all(
        FAMILIES.map(async (family) => [family, await this.api.list(family, true)] as const),
      );
      this.catalogs.set({
        status: 'loaded',
        items: Object.fromEntries(loaded) as Partial<Record<CatalogFamily, CatalogItem[]>>,
      });
    } catch (error) {
      // No local fallback: a silently divergent vocabulary is worse than a visible failure.
      this.catalogs.set({ status: 'error', items: {}, error: toAppError(error) });
    }
  }

  private async refresh(family: CatalogFamily): Promise<void> {
    const items = await this.api.list(family, true);
    const current = this.catalogs();
    this.catalogs.set({
      ...current,
      status: 'loaded',
      items: { ...current.items, [family]: items },
    });
  }
}
