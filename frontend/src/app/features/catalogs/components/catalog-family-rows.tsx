import type { ReactNode } from 'react';
import { CATALOG_FAMILY_ORDER, type CatalogFamilyKind } from './catalog-family-rows.logic';
import './catalog-family-rows.css';

interface Props {
  /** The host's picker for one family; the host keeps its own state and persistence. */
  renderRow: (kind: CatalogFamilyKind) => ReactNode;
  /** Shown once above the rows, e.g. the catalog status notice. */
  notice?: ReactNode;
}

/**
 * The layout every host uses for skills, languages, programs and tags: one bordered row per
 * family, in one order, with the picker label in a fixed column so the rows line up.
 */
export function CatalogFamilyRows({ renderRow, notice }: Props) {
  return (
    <div className="catalog-family-rows">
      {notice}
      {CATALOG_FAMILY_ORDER.map((kind) => (
        <div className="catalog-family-row" data-family={kind} key={kind}>
          {renderRow(kind)}
        </div>
      ))}
    </div>
  );
}
