import { useServices } from '../../core/di/services-context';
import { useSignal } from '../../core/state/use-signal';
import type { CatalogService } from './services/catalog.service';

/**
 * Returns the catalog service with its signal already subscribed.
 *
 * Components must never reach for `useServices().catalogService` directly during
 * render: the read (`activeNames`, `list`) and the subscription are separate
 * calls, so a bare read compiles, renders correctly once, and then silently
 * stops updating. Bundling them here makes that mistake impossible to express.
 */
export function useCatalogs(): CatalogService {
  const { catalogService } = useServices();
  useSignal(catalogService.catalogs);
  return catalogService;
}
