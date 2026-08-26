import { useState, type ChangeEvent, type FormEvent } from 'react';
import { useCatalogs } from '../../catalogs/use-catalogs';
import { CatalogStatusNotice } from '../../catalogs/components/catalog-status';
import { useCatalogStatus } from '../../catalogs/components/use-catalog-status';
import type { CandidateStatus } from '../../candidates/models/candidate.models';
import {
  ALL_CANDIDATE_STATUSES,
  type CriteriaFilter,
  type SearchFilters as SearchFiltersModel,
} from '../models/search.models';
import { CriteriaGroup } from './criteria-group';
import { CRITERIA_GROUPS, type CriteriaKind } from './criteria-group.model';
import { FiltersSummary } from './filters-summary';
import { buildSummaryGroups } from './filters-summary.logic';
import './search-filters.css';

const STATUS_LABELS: Record<CandidateStatus, string> = {
  new: 'Nuevo',
  available: 'Disponible',
  in_process: 'En proceso',
  hired: 'Contratado',
  rejected: 'Descartado',
};

const STATUS_OPTIONS = ALL_CANDIDATE_STATUSES.map((value) => ({
  value,
  label: STATUS_LABELS[value],
}));

const EMPTY_DRAFTS: Record<CriteriaKind, CriteriaFilter> = {
  skill: { value: '', level: '' },
  language: { value: '', level: '' },
  program: { value: '', level: '' },
};

interface SearchFiltersProps {
  filters: SearchFiltersModel;
  onFiltersChange: (next: SearchFiltersModel) => void;
  collapsed: boolean;
  onCollapsedChange: (next: boolean) => void;
  onSearch: (filters: SearchFiltersModel) => void;
  onClear: () => void;
}

/**
 * Controlled component. The Angular original mutated its @Input in place and
 * relied on sharing the object reference with the parent; every write here is an
 * immutable update pushed back through onFiltersChange.
 */
export function SearchFilters({
  filters,
  onFiltersChange,
  collapsed,
  onCollapsedChange,
  onSearch,
  onClear,
}: SearchFiltersProps) {
  const catalogs = useCatalogs();
  const catalogStatus = useCatalogStatus();
  const [drafts, setDrafts] = useState<Record<CriteriaKind, CriteriaFilter>>(EMPTY_DRAFTS);

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
    onSearch(filters);
  };

  return (
    <>
      <div className="filters-header">
        <h2>Parámetros de búsqueda</h2>
        <button
          className="button ghost small"
          type="button"
          data-testid="toggle-filters"
          aria-expanded={!collapsed}
          onClick={() => onCollapsedChange(!collapsed)}
        >
          {collapsed ? 'Mostrar filtros' : 'Ocultar filtros'}
        </button>
      </div>

      <form className="grid" onSubmit={submit} noValidate>
        <FiltersSummary groups={buildSummaryGroups(filters, STATUS_OPTIONS)} />

        {!collapsed ? (
          <div className="filters-body grid">
            <div className="grid two">
              <div className="basic-filters">
                <div className="inline-field">
                  <label htmlFor="filter-text">Texto</label>
                  <input
                    id="filter-text"
                    name="text"
                    value={filters.text}
                    placeholder="Nombre, email, notas..."
                    onChange={(e) => onFiltersChange({ ...filters, text: e.target.value })}
                  />
                </div>
                <div className="inline-field">
                  <label htmlFor="filter-hasCv">CV</label>
                  <select
                    id="filter-hasCv"
                    name="hasCv"
                    value={filters.hasCv}
                    onChange={(e) =>
                      onFiltersChange({
                        ...filters,
                        hasCv: e.target.value as SearchFiltersModel['hasCv'],
                      })
                    }
                  >
                    <option value="">Todos</option>
                    <option value="yes">Con CV</option>
                    <option value="no">Sin CV</option>
                  </select>
                </div>
              </div>
              <fieldset className="status-group">
                <legend>Estados</legend>
                <div className="status-options">
                  {STATUS_OPTIONS.map((option) => (
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

        <div className="toolbar">
          <button className="button" type="submit">
            Buscar
          </button>
          <button className="button secondary" type="button" onClick={onClear}>
            Limpiar
          </button>
        </div>
      </form>
    </>
  );
}
