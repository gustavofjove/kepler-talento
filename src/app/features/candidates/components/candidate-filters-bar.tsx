import { useTranslation } from 'react-i18next';
import type { CandidateStatus } from '../models/candidate.models';
import {
  type CandidateFilters,
  type FilterChip,
  type HasCvFilter,
  STATUS_OPTIONS,
  statusLabel,
} from '../pages/candidate-list.logic';

interface Props {
  filters: CandidateFilters;
  chips: FilterChip[];
  /**
   * Whether the actor may include removed candidates. Without it the control is absent, not
   * merely disabled: the API refuses the option for them (KTL-18 design D3).
   */
  canIncludeInactive: boolean;
  onPatch: (patch: Partial<CandidateFilters>) => void;
  onClear: () => void;
  onRemoveChip: (key: string) => void;
}

export function CandidateFiltersBar({
  filters,
  chips,
  canIncludeInactive,
  onPatch,
  onClear,
  onRemoveChip,
}: Props) {
  const { t } = useTranslation();
  return (
    <>
      <div className="panel filters-bar" data-testid="candidate-filters">
        <div className="field">
          <label htmlFor="filter-text">{t('candidates.list.filter.text')}</label>
          <input
            id="filter-text"
            name="text"
            value={filters.textFilter}
            placeholder={t('candidates.list.filter.textPlaceholder')}
            onChange={(event) => onPatch({ textFilter: event.target.value })}
          />
        </div>
        <div className="field">
          <label htmlFor="filter-status">{t('candidates.list.filter.status')}</label>
          <select
            id="filter-status"
            name="status"
            value={filters.statusFilter}
            onChange={(event) =>
              onPatch({ statusFilter: event.target.value as CandidateStatus | '' })
            }
          >
            <option value="">{t('candidates.list.filter.all')}</option>
            {STATUS_OPTIONS.map((status) => (
              <option key={status} value={status}>
                {statusLabel(status, t)}
              </option>
            ))}
          </select>
        </div>
        <div className="field">
          <label htmlFor="filter-hasCv">{t('candidates.list.filter.cv')}</label>
          <select
            id="filter-hasCv"
            name="hasCv"
            value={filters.hasCvFilter}
            onChange={(event) => onPatch({ hasCvFilter: event.target.value as HasCvFilter })}
          >
            <option value="">{t('candidates.list.filter.all')}</option>
            <option value="yes">{t('candidates.list.filter.withCv')}</option>
            <option value="no">{t('candidates.list.filter.withoutCv')}</option>
          </select>
        </div>
        {canIncludeInactive ? (
          <label className="inline-check">
            <input
              name="includeInactive"
              type="checkbox"
              checked={filters.includeInactive}
              onChange={(event) => onPatch({ includeInactive: event.target.checked })}
            />
            {t('candidates.list.filter.includeInactive')}
          </label>
        ) : null}
        <div className="filters-actions">
          <button className="button secondary" type="button" onClick={onClear}>
            {t('candidates.list.filter.clear')}
          </button>
        </div>
      </div>

      {chips.length ? (
        <div className="panel">
          <div className="toolbar">
            <strong>{t('candidates.list.filter.active')}</strong>
            <div className="form-actions">
              {chips.map((chip) => (
                <button
                  className="button ghost"
                  type="button"
                  key={chip.key}
                  onClick={() => onRemoveChip(chip.key)}
                >
                  {t('candidates.list.chip.remove', { label: chip.label })}
                </button>
              ))}
            </div>
          </div>
        </div>
      ) : null}
    </>
  );
}
