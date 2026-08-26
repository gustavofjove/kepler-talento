import type { ApiTransport } from '../../../core/http/api-transport';
import type { CatalogFamily, CatalogItem } from '../models/catalog.models';

export interface CatalogItemPayload {
  nameEs: string;
  code?: string;
  nameEn?: string;
}

/**
 * The catalog contract the service depends on. There is deliberately no delete call:
 * a value is removed from use by being deactivated. Tests substitute this interface
 * rather than the network.
 */
export interface CatalogGateway {
  list(family: CatalogFamily, includeInactive: boolean): Promise<CatalogItem[]>;
  create(family: CatalogFamily, payload: CatalogItemPayload): Promise<CatalogItem>;
  update(
    family: CatalogFamily,
    id: string,
    payload: CatalogItemPayload & { version: number },
  ): Promise<CatalogItem>;
  reorder(family: CatalogFamily, orderedIds: string[]): Promise<CatalogItem[]>;
  setActive(
    family: CatalogFamily,
    id: string,
    isActive: boolean,
    version: number,
  ): Promise<CatalogItem>;
}

/** The HTTP implementation, over the shared API transport. */
export class CatalogApi implements CatalogGateway {
  constructor(private readonly transport: ApiTransport) {}

  list(family: CatalogFamily, includeInactive: boolean): Promise<CatalogItem[]> {
    return this.transport.request<CatalogItem[]>(
      `/catalogs/${encodeURIComponent(family)}?includeInactive=${includeInactive}`,
    );
  }

  create(family: CatalogFamily, payload: CatalogItemPayload): Promise<CatalogItem> {
    return this.transport.request<CatalogItem>(`/catalogs/${encodeURIComponent(family)}`, {
      method: 'POST',
      body: JSON.stringify(payload),
    });
  }

  update(
    family: CatalogFamily,
    id: string,
    payload: CatalogItemPayload & { version: number },
  ): Promise<CatalogItem> {
    return this.transport.request<CatalogItem>(
      `/catalogs/${encodeURIComponent(family)}/${encodeURIComponent(id)}`,
      { method: 'PUT', body: JSON.stringify(payload) },
    );
  }

  reorder(family: CatalogFamily, orderedIds: string[]): Promise<CatalogItem[]> {
    return this.transport.request<CatalogItem[]>(`/catalogs/${encodeURIComponent(family)}/order`, {
      method: 'PUT',
      body: JSON.stringify({ orderedIds }),
    });
  }

  setActive(
    family: CatalogFamily,
    id: string,
    isActive: boolean,
    version: number,
  ): Promise<CatalogItem> {
    return this.transport.request<CatalogItem>(
      `/catalogs/${encodeURIComponent(family)}/${encodeURIComponent(id)}/active`,
      { method: 'PUT', body: JSON.stringify({ isActive, version }) },
    );
  }
}
