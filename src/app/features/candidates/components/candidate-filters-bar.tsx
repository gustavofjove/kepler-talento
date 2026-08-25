import type { CandidateStatus } from '../models/candidate.models';
import {
  type CandidateFilters,
  type FilterChip,
  type HasCvFilter,
  STATUS_OPTIONS,
} from '../pages/candidate-list.logic';

interface Props {
  filters: CandidateFilters;
  chips: FilterChip[];
  onPatch: (patch: Partial<CandidateFilters>) => void;
  onClear: () => void;
  onRemoveChip: (key: string) => void;
}

export function CandidateFiltersBar({ filters, chips, onPatch, onClear, onRemoveChip }: Props) {
  return (
    <>
      <div className="panel filters-bar">
        <div className="field">
          <label htmlFor="filter-text">Texto</label>
          <input
            id="filter-text"
            name="text"
            value={filters.textFilter}
            placeholder="Nombre, email, teléfono"
            onChange={(event) => onPatch({ textFilter: event.target.value })}
          />
        </div>
        <div className="field">
          <label htmlFor="filter-status">Estado</label>
          <select
            id="filter-status"
            name="status"
            value={filters.statusFilter}
            onChange={(event) =>
              onPatch({ statusFilter: event.target.value as CandidateStatus | '' })
            }
          >
            <option value="">Todos</option>
            {STATUS_OPTIONS.map((status) => (
              <option key={status} value={status}>
                {status}
              </option>
            ))}
          </select>
        </div>
        <div className="field">
          <label htmlFor="filter-hasCv">CV</label>
          <select
            id="filter-hasCv"
            name="hasCv"
            value={filters.hasCvFilter}
            onChange={(event) => onPatch({ hasCvFilter: event.target.value as HasCvFilter })}
          >
            <option value="">Todos</option>
            <option value="yes">Con CV</option>
            <option value="no">Sin CV</option>
          </select>
        </div>
        <label className="inline-check">
          <input
            name="includeInactive"
            type="checkbox"
            checked={filters.includeInactive}
            onChange={(event) => onPatch({ includeInactive: event.target.checked })}
          />
          Incluir inactivos
        </label>
        <div className="filters-actions">
          <button className="button secondary" type="button" onClick={onClear}>
            Limpiar
          </button>
        </div>
      </div>

      {chips.length ? (
        <div className="panel">
          <div className="toolbar">
            <strong>Filtros activos</strong>
            <div className="form-actions">
              {chips.map((chip) => (
                <button
                  className="button ghost"
                  type="button"
                  key={chip.key}
                  onClick={() => onRemoveChip(chip.key)}
                >
                  {chip.label} ×
                </button>
              ))}
            </div>
          </div>
        </div>
      ) : null}
    </>
  );
}
