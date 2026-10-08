import { useTranslation } from 'react-i18next';
import { useCatalogs } from '../../catalogs/use-catalogs';
import type { SearchFilters } from '../models/search.models';
import { buildSummaryGroups } from './search-criteria.logic';
import './search-filters.css';

interface SearchCriteriaSummaryProps {
  filters: SearchFilters;
  /**
   * `columns` (default) lays the groups out in titled columns; `inline` puts every group on one
   * wrapping line of label + chips, for places with little height such as a table row (KTL-31).
   */
  layout?: 'columns' | 'inline';
}

/**
 * The read-only view of a filter set. The one component that renders it: the search page,
 * the preset list and the position page all use this, so a change here reaches all of them.
 */
export function SearchCriteriaSummary({ filters, layout = 'columns' }: SearchCriteriaSummaryProps) {
  const { t } = useTranslation();
  // Not memoised: the catalog colours are read through the subscribed service, so the summary
  // is rebuilt (cheaply) on the re-render a recolour triggers rather than kept stale.
  const catalogs = useCatalogs();
  const groups = buildSummaryGroups(filters, t, (family, name) => catalogs.colorOf(family, name));
  const inline = layout === 'inline';

  return (
    <div
      className={inline ? 'filters-summary filters-summary--inline' : 'filters-summary'}
      data-testid="filters-summary"
      aria-label={t('search.criteria.summary.label')}
    >
      {groups.length ? (
        groups.map((group) =>
          inline ? (
            <span className="summary-group" key={group.label}>
              <span className="summary-label">{group.label}</span>
              {group.values.map((value) => (
                <span className="chip" key={value.text} data-catalog-color={value.color}>
                  {value.text}
                </span>
              ))}
            </span>
          ) : (
            <div className="summary-group" key={group.label}>
              <h3>{group.label}</h3>
              <div className="summary-values">
                {group.values.map((value) => (
                  <span className="chip" key={value.text} data-catalog-color={value.color}>
                    {value.text}
                  </span>
                ))}
              </div>
            </div>
          ),
        )
      ) : (
        <p className="empty-state">{t('search.criteria.summary.empty')}</p>
      )}
    </div>
  );
}
