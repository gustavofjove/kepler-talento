import { useCatalogs } from '../use-catalogs';

export interface CatalogStatus {
  /** True until the API-backed vocabulary has loaded. */
  isLoading: boolean;
  hasFailed: boolean;
  /** Spanish message to show while the options cannot be offered, or null. */
  message: string | null;
}

/**
 * Tells a component whether its empty option list means "still loading", "failed",
 * or "genuinely empty", so it never presents a partial load as a complete result.
 */
export function useCatalogStatus(): CatalogStatus {
  const catalogs = useCatalogs();
  const isLoading = catalogs.status === 'idle' || catalogs.status === 'loading';
  const hasFailed = catalogs.status === 'error';
  return {
    isLoading,
    hasFailed,
    message: isLoading
      ? 'Cargando opciones…'
      : hasFailed
        ? (catalogs.error?.message ?? 'No se han podido cargar las opciones.')
        : null,
  };
}
