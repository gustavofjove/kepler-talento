import { useMemo } from 'react';
import { useTranslation } from 'react-i18next';
import type { SearchFilters } from '../models/search.models';
import { buildSummaryGroups } from './search-criteria.logic';
import './search-filters.css';

/**
 * The read-only view of a filter set. The one component that renders it: the search page,
 * the preset list and the preset detail all use this, so a change here reaches all three.
 */
export function SearchCriteriaSummary({ filters }: { filters: SearchFilters }) {
  const { t } = useTranslation();
  const groups = useMemo(() => buildSummaryGroups(filters, t), [filters, t]);

  return (
    <div
      className="filters-summary"
      data-testid="filters-summary"
      aria-label={t('search.criteria.summary.label')}
    >
      {groups.length ? (
        groups.map((group) => (
          <div className="summary-group" key={group.label}>
            <h3>{group.label}</h3>
            <div className="summary-values">
              {group.values.map((value) => (
                <span className="chip" key={value}>
                  {value}
                </span>
              ))}
            </div>
          </div>
        ))
      ) : (
        <p className="empty-state">{t('search.criteria.summary.empty')}</p>
      )}
    </div>
  );
}
