import {
  useEffect,
  useId,
  useLayoutEffect,
  useMemo,
  useRef,
  useState,
  type FormEvent,
  type ReactNode,
} from 'react';
import { useTranslation } from 'react-i18next';
import { useCatalogs } from '../../catalogs/use-catalogs';
import { CatalogFamilyRows } from '../../catalogs/components/catalog-family-rows';
import { CatalogStatusNotice } from '../../catalogs/components/catalog-status';
import { useCatalogStatus } from '../../catalogs/components/use-catalog-status';
import type { CandidateAvailabilityState } from '../../candidates/models/candidate.models';
import type { CriteriaFilter, SearchFilters } from '../models/search.models';
import { CriteriaGroup } from './criteria-group';
import { CRITERIA_GROUPS, type CriteriaKind } from './criteria-group.model';
import { availabilityOptions } from './search-criteria.logic';
import {
  cvChoices,
  selectedAvailability,
  toggleAvailability,
  toggleCv,
} from './search-basic-filters.logic';
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
 * The criteria editor shared by search, preset and position pages.
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
  const options = useMemo(() => availabilityOptions(t), [t]);
  const availabilityValues = useMemo(() => options.map((option) => option.value), [options]);
  const selected = selectedAvailability(filters.availabilityValues, availabilityValues);
  const cv = cvChoices(filters.hasCv);
  const [availabilityOpen, setAvailabilityOpen] = useState(false);
  const availabilityButtonRef = useRef<HTMLButtonElement>(null);
  const availabilityPanelRef = useRef<HTMLDivElement>(null);
  const availabilityPanelId = useId();
  const checkedFromId = useId();
  const availabilitySummary =
    selected.length === options.length
      ? t('search.criteria.availability.all')
      : selected.length === 1
        ? options.find((option) => option.value === selected[0])?.label
        : t('search.criteria.availability.count', { count: selected.length });

  const collapsible = onCollapsedChange !== undefined;
  const isCollapsed = collapsible && Boolean(collapsed);

  useEffect(() => {
    if (!availabilityOpen) return;

    const onPointerDown = (event: PointerEvent): void => {
      const target = event.target;
      if (!(target instanceof Node)) return;
      if (
        availabilityButtonRef.current?.contains(target) ||
        availabilityPanelRef.current?.contains(target)
      )
        return;
      setAvailabilityOpen(false);
    };
    const onKeyDown = (event: KeyboardEvent): void => {
      if (event.key !== 'Escape') return;
      const focused = document.activeElement;
      if (
        focused !== availabilityButtonRef.current &&
        !availabilityPanelRef.current?.contains(focused)
      )
        return;
      setAvailabilityOpen(false);
      availabilityButtonRef.current?.focus();
    };

    document.addEventListener('pointerdown', onPointerDown);
    document.addEventListener('keydown', onKeyDown);
    return () => {
      document.removeEventListener('pointerdown', onPointerDown);
      document.removeEventListener('keydown', onKeyDown);
    };
  }, [availabilityOpen]);

  useLayoutEffect(() => {
    if (!availabilityOpen) return;
    const placePanel = (): void => {
      const button = availabilityButtonRef.current;
      const panel = availabilityPanelRef.current;
      if (!button || !panel) return;
      const buttonLeft = button.getBoundingClientRect().left;
      const panelWidth = panel.getBoundingClientRect().width;
      const viewportWidth = document.documentElement.clientWidth;
      const left = Math.max(16, Math.min(buttonLeft, viewportWidth - panelWidth - 16));
      panel.style.left = `${left - buttonLeft}px`;
    };
    placePanel();
    window.addEventListener('resize', placePanel);
    return () => window.removeEventListener('resize', placePanel);
  }, [availabilityOpen]);

  const changeAvailability = (state: CandidateAvailabilityState, checked: boolean): void => {
    const next = toggleAvailability(filters.availabilityValues, availabilityValues, state, checked);
    if (next.length === selected.length && next.every((value, index) => value === selected[index]))
      return;
    onFiltersChange({ ...filters, availabilityValues: next });
  };

  // A value appears once per family: the picker never offers one already held, so its
  // level is changed on its chip rather than by adding it again.
  const setCriteria = (kind: CriteriaKind, criteria: CriteriaFilter[]): void => {
    onFiltersChange({ ...filters, [`${kind}Criteria`]: criteria });
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

        {/* The chips already show every criterion while the body is open. */}
        {isCollapsed ? <SearchCriteriaSummary filters={filters} /> : null}

        {!isCollapsed ? (
          <div className="filters-body grid">
            <div className="basic-filters-row">
              <div className="inline-field basic-text-field">
                <label htmlFor="filter-text">{t('search.criteria.text')}</label>
                <input
                  id="filter-text"
                  name="text"
                  value={filters.text}
                  placeholder={t('search.criteria.textPlaceholder')}
                  onChange={(e) => onFiltersChange({ ...filters, text: e.target.value })}
                />
              </div>
              <div className="basic-cv-field" role="group" aria-label={t('search.criteria.cv')}>
                <span className="basic-field-label">{t('search.criteria.cv')}</span>
                {(['yes', 'no'] as const).map((choice) => (
                  <label className="inline-check" key={choice}>
                    <input
                      type="checkbox"
                      name="hasCv"
                      value={choice}
                      checked={cv[choice]}
                      onChange={(event) => {
                        const next = toggleCv(filters.hasCv, choice, event.target.checked);
                        if (next !== filters.hasCv) onFiltersChange({ ...filters, hasCv: next });
                      }}
                    />
                    {t(`search.criteria.cv.${choice}`)}
                  </label>
                ))}
              </div>
              <div className="availability-picker">
                <button
                  ref={availabilityButtonRef}
                  type="button"
                  className="availability-disclosure"
                  data-testid="availability-disclosure"
                  aria-expanded={availabilityOpen}
                  aria-controls={availabilityPanelId}
                  onClick={() => setAvailabilityOpen((open) => !open)}
                >
                  <span className="basic-field-label">
                    {t('search.criteria.availability.label')}
                  </span>
                  <span>{availabilitySummary}</span>
                  <span aria-hidden="true">{availabilityOpen ? '▴' : '▾'}</span>
                </button>
                {availabilityOpen ? (
                  <div
                    ref={availabilityPanelRef}
                    id={availabilityPanelId}
                    className="availability-panel"
                    role="group"
                    aria-label={t('search.criteria.availability.label')}
                  >
                    <div className="availability-options">
                      {options.map((option) => (
                        <div className="availability-option" key={option.value}>
                          <label className="inline-check">
                            <input
                              type="checkbox"
                              name="availabilityValues"
                              data-availability={option.value}
                              checked={selected.includes(option.value)}
                              onChange={(event) =>
                                changeAvailability(option.value, event.target.checked)
                              }
                            />
                            {option.label}
                          </label>
                          <button
                            type="button"
                            className="availability-only"
                            data-testid={`availability-only-${option.value}`}
                            data-selected={selected.length === 1 && selected[0] === option.value}
                            aria-label={t('search.criteria.availability.only', {
                              value: option.label,
                            })}
                            title={t('search.criteria.availability.only', { value: option.label })}
                            onClick={() =>
                              onFiltersChange({ ...filters, availabilityValues: [option.value] })
                            }
                          >
                            <span className="availability-only-icon" aria-hidden="true" />
                          </button>
                        </div>
                      ))}
                    </div>
                    {selected.length < options.length ? (
                      <button
                        type="button"
                        className="button ghost small availability-select-all"
                        data-testid="availability-select-all"
                        onClick={() => onFiltersChange({ ...filters, availabilityValues })}
                      >
                        {t('search.criteria.availability.selectAll')}
                      </button>
                    ) : null}
                  </div>
                ) : null}
              </div>
              <div className="inline-field basic-checked-from-field">
                <label htmlFor={checkedFromId}>{t('search.criteria.checkedFrom.label')}</label>
                <input
                  id={checkedFromId}
                  name="availabilityCheckedFrom"
                  type="date"
                  data-testid="availability-checked-from"
                  value={filters.availabilityCheckedFrom}
                  onChange={(event) =>
                    onFiltersChange({ ...filters, availabilityCheckedFrom: event.target.value })
                  }
                />
              </div>
            </div>

            <CatalogFamilyRows
              notice={<CatalogStatusNotice status={catalogStatus} />}
              renderRow={(kind) => {
                const group = CRITERIA_GROUPS[kind];
                return (
                  <CriteriaGroup
                    group={group}
                    criteria={filters[`${kind}Criteria`]}
                    mode={filters[`${kind}Mode`]}
                    valueOptions={catalogs.activeNames(group.valueFamily)}
                    levelOptions={group.levelFamily ? catalogs.activeNames(group.levelFamily) : []}
                    disabled={Boolean(catalogStatus.message)}
                    onCriteriaChange={(criteria) => setCriteria(kind, criteria)}
                    onModeChange={(mode) => onFiltersChange({ ...filters, [`${kind}Mode`]: mode })}
                  />
                );
              }}
            />
          </div>
        ) : null}

        <div className="toolbar">{actions}</div>
      </form>
    </>
  );
}
