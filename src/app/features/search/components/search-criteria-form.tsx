import { useMemo, useState, type ChangeEvent, type FormEvent, type ReactNode } from 'react';
import { useTranslation } from 'react-i18next';
import { useCatalogs } from '../../catalogs/use-catalogs';
import { CatalogStatusNotice } from '../../catalogs/components/catalog-status';
import { useCatalogStatus } from '../../catalogs/components/use-catalog-status';
import type { CandidateStatus } from '../../candidates/models/candidate.models';
import type { CriteriaFilter, SearchFilters } from '../models/search.models';
import { CriteriaGroup } from './criteria-group';
import { CRITERIA_GROUPS, type CriteriaKind } from './criteria-group.model';
import { EMPTY_DRAFTS, statusOptions } from './search-criteria.logic';
import { SearchCriteriaSummary } from './search-criteria-summary';
import './search-filters.css';

interface SearchCriteriaFormProps {
  filters: SearchFilters;
  onFiltersChange: (next: SearchFilters) => void;
  onSubmit: (filters: SearchFilters) => void;
  /** The host's own buttons: searching on the search page, saving on the preset pages. */
  actions: ReactNode;
  /** Rendered inside the form before the criteria, e.g. the preset name field. */
  leading?: ReactNode;
  /** Collapsing is offered only when the host controls it. Omitted, the body always shows. */
  collapsed?: boolean;
  onCollapsedChange?: (next: boolean) => void;
}

/**
 * The criteria editor. The one component that edits a filter set: the search page and the
 * preset create and edit pages all render it, so a change here reaches every screen.
 *
 * Controlled: every write is an immutable update pushed back through `onFiltersChange`, and
 * nothing here runs a search or saves a preset - that is the host's `onSubmit`.
 */
export function SearchCriteriaForm({
  filters,
  onFiltersChange,
  onSubmit,
  actions,
  leading,
  collapsed,
  onCollapsedChange,
}: SearchCriteriaFormProps) {
  const { t } = useTranslation();
  const catalogs = useCatalogs();
  const catalogStatus = useCatalogStatus();
  const [drafts, setDrafts] = useState<Record<CriteriaKind, CriteriaFilter>>(EMPTY_DRAFTS);
  const options = useMemo(() => statusOptions(t), [t]);

  const collapsible = onCollapsedChange !== undefined;
  const isCollapsed = collapsible && Boolean(collapsed);

  const toggleStatus = (status: CandidateStatus, event: ChangeEvent<HTMLInputElement>): void => {
    onFiltersChange({
      ...filters,
      statusValues: event.target.checked
        ? [...filters.statusValues, status]
        : filters.statusValues.filter((item) => item !== status),
    });
  };

  const addCriterion = (kind: CriteriaKind): void => {
    const draft = drafts[kind];
    if (!draft.value) {
      return;
    }
    // A value can only appear once per type: adding it again replaces its level.
    const criteria = filters[`${kind}Criteria`];
    const index = criteria.findIndex((item) => item.value === draft.value);
    onFiltersChange({
      ...filters,
      [`${kind}Criteria`]:
        index >= 0
          ? criteria.map((item, i) => (i === index ? { ...draft } : item))
          : [...criteria, { ...draft }],
    });
    setDrafts((current) => ({ ...current, [kind]: { value: '', level: '' } }));
  };

  const submit = (event: FormEvent<HTMLFormElement>): void => {
    event.preventDefault();
    onSubmit(filters);
  };

  return (
    <>
      <div className="filters-header">
        <h2>{t('search.criteria.title')}</h2>
        {collapsible ? (
          <button
            className="button ghost small"
            type="button"
            data-testid="toggle-filters"
            aria-expanded={!isCollapsed}
            onClick={() => onCollapsedChange(!isCollapsed)}
          >
            {isCollapsed ? t('search.criteria.toggle.show') : t('search.criteria.toggle.hide')}
          </button>
        ) : null}
      </div>

      <form className="grid" onSubmit={submit} noValidate>
        {leading}

        <SearchCriteriaSummary filters={filters} />

        {!isCollapsed ? (
          <div className="filters-body grid">
            <div className="grid two">
              <div className="basic-filters">
                <div className="inline-field">
                  <label htmlFor="filter-text">{t('search.criteria.text')}</label>
                  <input
                    id="filter-text"
                    name="text"
                    value={filters.text}
                    placeholder={t('search.criteria.textPlaceholder')}
                    onChange={(e) => onFiltersChange({ ...filters, text: e.target.value })}
                  />
                </div>
                <div className="inline-field">
                  <label htmlFor="filter-hasCv">{t('search.criteria.cv')}</label>
                  <select
                    id="filter-hasCv"
                    name="hasCv"
                    value={filters.hasCv}
                    onChange={(e) =>
                      onFiltersChange({
                        ...filters,
                        hasCv: e.target.value as SearchFilters['hasCv'],
                      })
                    }
                  >
                    <option value="">{t('search.criteria.cv.any')}</option>
                    <option value="yes">{t('search.criteria.cv.yes')}</option>
                    <option value="no">{t('search.criteria.cv.no')}</option>
                  </select>
                </div>
              </div>
              <fieldset className="status-group">
                <legend>{t('search.criteria.statuses')}</legend>
                <div className="status-options">
                  {options.map((option) => (
                    <label className="inline-check" key={option.value}>
                      <input
                        type="checkbox"
                        data-status={option.value}
                        checked={filters.statusValues.includes(option.value)}
                        onChange={(e) => toggleStatus(option.value, e)}
                      />
                      {option.label}
                    </label>
                  ))}
                </div>
              </fieldset>
            </div>

            <CatalogStatusNotice status={catalogStatus} />

            {CRITERIA_GROUPS.map((group) => (
              <CriteriaGroup
                key={group.kind}
                group={group}
                criteria={filters[`${group.kind}Criteria`]}
                mode={filters[`${group.kind}Mode`]}
                draft={drafts[group.kind]}
                valueOptions={catalogs.activeNames(group.valueFamily)}
                levelOptions={catalogs.activeNames(group.levelFamily)}
                onDraftChange={(draft) =>
                  setDrafts((current) => ({ ...current, [group.kind]: draft }))
                }
                onAdd={() => addCriterion(group.kind)}
                onRemove={(index) =>
                  onFiltersChange({
                    ...filters,
                    [`${group.kind}Criteria`]: filters[`${group.kind}Criteria`].filter(
                      (_, i) => i !== index,
                    ),
                  })
                }
                onModeChange={(mode) =>
                  onFiltersChange({ ...filters, [`${group.kind}Mode`]: mode })
                }
              />
            ))}
          </div>
        ) : null}

        <div className="toolbar">{actions}</div>
      </form>
    </>
  );
}
