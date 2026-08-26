import type { CatalogStatus } from './use-catalog-status';

/** Renders the loading or failure line for catalog-backed options, or nothing. */
export function CatalogStatusNotice({ status }: { status: CatalogStatus }) {
  if (!status.message) {
    return null;
  }
  return (
    <p className="empty-state" data-testid="catalog-status">
      {status.message}
    </p>
  );
}
