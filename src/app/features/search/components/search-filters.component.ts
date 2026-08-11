import { Component, EventEmitter, Input, Output } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { CandidateStatus } from '../../candidates/models/candidate.models';
import { CatalogFamily } from '../../catalogs/models/catalog.models';
import { CatalogService } from '../../catalogs/services/catalog.service';
import {
  ALL_CANDIDATE_STATUSES,
  CriteriaFilter,
  MultiValueMode,
  SearchFilters,
} from '../models/search.models';

type CriteriaKind = 'skill' | 'language' | 'program';

interface CriteriaGroup {
  kind: CriteriaKind;
  label: string;
  levelLabel: string;
  addLabel: string;
  valueFamily: CatalogFamily;
  levelFamily: CatalogFamily;
}

const CRITERIA_GROUPS: CriteriaGroup[] = [
  {
    kind: 'skill',
    label: 'Habilidad',
    levelLabel: 'Nivel de habilidad',
    addLabel: 'Añadir habilidad',
    valueFamily: 'skill',
    levelFamily: 'skill_level',
  },
  {
    kind: 'language',
    label: 'Idiomas',
    levelLabel: 'Nivel de idioma',
    addLabel: 'Añadir idioma',
    valueFamily: 'language',
    levelFamily: 'language_level',
  },
  {
    kind: 'program',
    label: 'Programas',
    levelLabel: 'Nivel de programa',
    addLabel: 'Añadir programa',
    valueFamily: 'program',
    levelFamily: 'program_level',
  },
];

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

@Component({
  selector: 'rrhh-search-filters',
  standalone: true,
  imports: [FormsModule],
  styles: [
    `
      .filters-header {
        align-items: center;
        display: flex;
        gap: 12px;
        justify-content: space-between;
      }
      .filters-header h2 {
        margin: 0;
      }
      /* Grouped into columns so a long filter set stays short instead of stacking up. */
      .filters-summary {
        display: grid;
        gap: 12px 20px;
        grid-template-columns: repeat(auto-fit, minmax(240px, 1fr));
        margin: 12px 0;
      }
      .summary-group {
        display: grid;
        gap: 6px;
      }
      .summary-group h3 {
        color: var(--fg-2);
        font-size: 11px;
        font-weight: 600;
        margin: 0;
      }
      .summary-values {
        display: flex;
        flex-wrap: wrap;
        gap: 6px;
      }
      .filters-summary .empty-state {
        grid-column: 1 / -1;
        margin: 0;
      }
      .filters-body {
        border-top: 1px solid var(--border);
        padding-top: 14px;
      }
      .basic-filters {
        align-content: start;
        display: grid;
        gap: 10px;
      }
      .inline-field {
        align-items: center;
        display: flex;
        gap: 10px;
      }
      .inline-field label {
        color: var(--fg-2);
        flex: 0 0 auto;
        font-size: 11px;
        font-weight: 600;
        min-width: 48px;
      }
      /* The global .field rules do not reach here, so match their look explicitly. */
      .inline-field input,
      .inline-field select {
        background: var(--bg-1);
        border: 1px solid var(--border);
        border-radius: var(--r-md);
        flex: 1 1 auto;
        min-height: 34px;
        padding: 8px 10px;
        width: 100%;
      }
      .inline-field input:focus,
      .inline-field select:focus {
        border-color: var(--fj-navy);
        outline: 2px solid rgba(22, 33, 54, 0.15);
        outline-offset: 0;
      }
      .status-group,
      .criteria-group {
        border: 1px solid var(--border);
        border-radius: var(--r-md);
        display: grid;
        gap: 10px;
        padding: 12px;
      }
      .status-group legend {
        color: var(--fg-2);
        font-size: 11px;
        font-weight: 600;
        padding: 0 4px;
      }
      .status-options {
        display: flex;
        flex-wrap: wrap;
        gap: 8px 16px;
      }
      .criteria-add {
        align-items: end;
        display: grid;
        gap: 8px;
        grid-template-columns: 1fr 1fr auto;
      }
      .criteria-list {
        display: grid;
        gap: 6px;
        list-style: none;
        margin: 0;
        padding: 0;
      }
      .criteria-list li {
        align-items: center;
        background: var(--bg-2, transparent);
        border: 1px solid var(--border);
        border-radius: var(--r-md);
        display: flex;
        gap: 8px;
        justify-content: space-between;
        padding: 6px 10px;
      }
      .criteria-mode {
        align-items: center;
        display: flex;
        gap: 8px;
      }
      .criteria-mode select {
        max-width: 180px;
      }
      @media (max-width: 720px) {
        .criteria-add {
          grid-template-columns: 1fr;
        }
      }
    `,
  ],
  template: `
    <div class="filters-header">
      <h2>Parámetros de búsqueda</h2>
      <button
        class="button ghost small"
        type="button"
        data-testid="toggle-filters"
        [attr.aria-expanded]="!collapsed"
        (click)="toggleCollapsed()"
      >
        {{ collapsed ? 'Mostrar filtros' : 'Ocultar filtros' }}
      </button>
    </div>

    <form class="grid" (ngSubmit)="search.emit(filters)">
      <div class="filters-summary" data-testid="filters-summary" aria-label="Resumen de filtros">
        @for (group of summaryGroups; track group.label) {
          <div class="summary-group">
            <h3>{{ group.label }}</h3>
            <div class="summary-values">
              @for (value of group.values; track value) {
                <span class="chip">{{ value }}</span>
              }
            </div>
          </div>
        } @empty {
          <p class="empty-state">Sin filtros aplicados.</p>
        }
      </div>

      @if (!collapsed) {
        <div class="filters-body grid">
          <div class="grid two">
            <div class="basic-filters">
              <div class="inline-field">
                <label for="filter-text">Texto</label>
                <input
                  id="filter-text"
                  name="text"
                  [(ngModel)]="filters.text"
                  placeholder="Nombre, email, notas..."
                />
              </div>
              <div class="inline-field">
                <label for="filter-hasCv">CV</label>
                <select id="filter-hasCv" name="hasCv" [(ngModel)]="filters.hasCv">
                  <option value="">Todos</option>
                  <option value="yes">Con CV</option>
                  <option value="no">Sin CV</option>
                </select>
              </div>
            </div>
            <fieldset class="status-group">
              <legend>Estados</legend>
              <div class="status-options">
                @for (option of statusOptions; track option.value) {
                  <label class="inline-check">
                    <input
                      type="checkbox"
                      [attr.data-status]="option.value"
                      [checked]="isStatusSelected(option.value)"
                      (change)="toggleStatus(option.value, $event)"
                    />
                    {{ option.label }}
                  </label>
                }
              </div>
            </fieldset>
          </div>

          @for (group of groups; track group.kind) {
            <fieldset
              class="criteria-group"
              [attr.data-criteria]="group.kind"
              [attr.aria-label]="group.label"
            >
              <div class="criteria-add">
                <div class="field">
                  <label [attr.for]="group.kind + '-value'">{{ group.label }}</label>
                  <select
                    [id]="group.kind + '-value'"
                    [name]="group.kind + 'Draft'"
                    [(ngModel)]="drafts[group.kind].value"
                  >
                    <option value="">Selecciona una opción</option>
                    @for (option of optionsFor(group.valueFamily); track option) {
                      <option [value]="option">{{ option }}</option>
                    }
                  </select>
                </div>
                <div class="field">
                  <label [attr.for]="group.kind + '-level'">{{ group.levelLabel }}</label>
                  <select
                    [id]="group.kind + '-level'"
                    [name]="group.kind + 'LevelDraft'"
                    [(ngModel)]="drafts[group.kind].level"
                  >
                    <option value="">Cualquier nivel</option>
                    @for (option of optionsFor(group.levelFamily); track option) {
                      <option [value]="option">{{ option }}</option>
                    }
                  </select>
                </div>
                <button
                  class="button secondary small"
                  type="button"
                  [disabled]="!drafts[group.kind].value"
                  [attr.data-testid]="'add-' + group.kind"
                  (click)="addCriterion(group.kind)"
                >
                  {{ group.addLabel }}
                </button>
              </div>

              @if (criteriaOf(group.kind).length) {
                <div class="criteria-mode">
                  <label [attr.for]="group.kind + '-mode'">Coincidencia</label>
                  <select
                    [id]="group.kind + '-mode'"
                    [name]="group.kind + 'Mode'"
                    [ngModel]="modeOf(group.kind)"
                    (ngModelChange)="setMode(group.kind, $event)"
                  >
                    <option value="ANY">Cualquiera</option>
                    <option value="ALL">Todos</option>
                  </select>
                </div>
                <ul class="criteria-list">
                  @for (criterion of criteriaOf(group.kind); track $index) {
                    <li>
                      <span>
                        <span class="badge">{{ criterion.value }}</span>
                        {{ criterion.level || 'Cualquier nivel' }}
                      </span>
                      <button
                        class="button ghost small"
                        type="button"
                        [attr.aria-label]="'Quitar ' + criterion.value"
                        (click)="removeCriterion(group.kind, $index)"
                      >
                        ×
                      </button>
                    </li>
                  }
                </ul>
              } @else {
                <p class="empty-state">Sin filtros de {{ group.label.toLowerCase() }}.</p>
              }
            </fieldset>
          }
        </div>
      }

      <div class="toolbar">
        <button class="button" type="submit">Buscar</button>
        <button class="button secondary" type="button" (click)="clear.emit()">Limpiar</button>
      </div>
    </form>
  `,
})
export class SearchFiltersComponent {
  @Input({ required: true }) filters!: SearchFilters;
  @Input() collapsed = false;
  @Output() readonly collapsedChange = new EventEmitter<boolean>();
  @Output() readonly search = new EventEmitter<SearchFilters>();
  @Output() readonly clear = new EventEmitter<void>();

  readonly groups = CRITERIA_GROUPS;
  readonly statusOptions = STATUS_OPTIONS;
  drafts: Record<CriteriaKind, CriteriaFilter> = {
    skill: { value: '', level: '' },
    language: { value: '', level: '' },
    program: { value: '', level: '' },
  };

  constructor(private readonly catalogs: CatalogService) {}

  isStatusSelected(status: CandidateStatus): boolean {
    return this.filters.statusValues.includes(status);
  }

  toggleStatus(status: CandidateStatus, event: Event): void {
    const checked = (event.target as HTMLInputElement).checked;
    this.filters.statusValues = checked
      ? [...this.filters.statusValues, status]
      : this.filters.statusValues.filter((item) => item !== status);
  }

  toggleCollapsed(): void {
    this.collapsed = !this.collapsed;
    this.collapsedChange.emit(this.collapsed);
  }

  optionsFor(family: CatalogFamily): string[] {
    return this.catalogs.activeNames(family);
  }

  criteriaOf(kind: CriteriaKind): CriteriaFilter[] {
    return this.filters[`${kind}Criteria`];
  }

  modeOf(kind: CriteriaKind): MultiValueMode {
    return this.filters[`${kind}Mode`];
  }

  setMode(kind: CriteriaKind, mode: MultiValueMode): void {
    this.filters[`${kind}Mode`] = mode === 'ALL' ? 'ALL' : 'ANY';
  }

  addCriterion(kind: CriteriaKind): void {
    const draft = this.drafts[kind];
    if (!draft.value) {
      return;
    }
    // A value can only appear once per type: adding it again replaces its level.
    const criteria = this.criteriaOf(kind);
    const index = criteria.findIndex((item) => item.value === draft.value);
    this.filters[`${kind}Criteria`] =
      index >= 0
        ? criteria.map((item, i) => (i === index ? { ...draft } : item))
        : [...criteria, { ...draft }];
    this.drafts[kind] = { value: '', level: '' };
  }

  removeCriterion(kind: CriteriaKind, index: number): void {
    this.filters[`${kind}Criteria`] = this.criteriaOf(kind).filter((_, i) => i !== index);
  }

  /** One block per filter type for the collapsed view, laid out in columns. */
  get summaryGroups(): Array<{ label: string; values: string[] }> {
    const groups: Array<{ label: string; values: string[] }> = [];

    if (this.filters.text.trim()) {
      groups.push({ label: 'Texto', values: [this.filters.text.trim()] });
    }
    if (this.filters.statusValues.length < this.statusOptions.length) {
      groups.push({
        label: 'Estados',
        values: this.filters.statusValues.map((status) => this.statusLabel(status)),
      });
    }
    if (this.filters.hasCv) {
      groups.push({
        label: 'CV',
        values: [this.filters.hasCv === 'yes' ? 'Con CV' : 'Sin CV'],
      });
    }
    this.groups.forEach((group) => {
      const criteria = this.criteriaOf(group.kind);
      if (!criteria.length) {
        return;
      }
      groups.push({
        label:
          criteria.length > 1
            ? `${group.label} (${this.modeLabel(this.modeOf(group.kind))})`
            : group.label,
        values: criteria.map(
          (criterion) => `${criterion.value}${criterion.level ? ` · ${criterion.level}` : ''}`,
        ),
      });
    });

    return groups;
  }

  private modeLabel(mode: MultiValueMode): string {
    return mode === 'ALL' ? 'Todos' : 'Cualquiera';
  }

  private statusLabel(status: string): string {
    return this.statusOptions.find((option) => option.value === status)?.label ?? status;
  }
}
