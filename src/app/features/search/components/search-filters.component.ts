import { Component, EventEmitter, Input, Output } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { CatalogService } from '../../catalogs/services/catalog.service';
import { SearchFilters } from '../models/search.models';

@Component({
  selector: 'rrhh-search-filters',
  standalone: true,
  imports: [FormsModule],
  template: `
    <form class="grid" (ngSubmit)="search.emit(filters)">
      @if (activeChips.length) {
        <div class="chip-row" aria-label="Filtros activos">
          @for (chip of activeChips; track chip.key) {
            <button
              class="chip"
              type="button"
              [attr.data-chip-key]="chip.key"
              (click)="removeChip(chip.key)"
            >
              {{ chip.label }} ×
            </button>
          }
        </div>
      }

      <div class="grid three">
        <div class="field">
          <label>Texto</label>
          <input name="text" [(ngModel)]="filters.text" placeholder="Nombre, email, notas..." />
        </div>
        <div class="field">
          <label>Estados</label>
          <select name="statusValues" multiple [(ngModel)]="filters.statusValues">
            <option value="new">Nuevo</option>
            <option value="available">Disponible</option>
            <option value="in_process">En proceso</option>
            <option value="hired">Contratado</option>
            <option value="rejected">Descartado</option>
          </select>
        </div>
        <div class="field">
          <label>CV</label>
          <select name="hasCv" [(ngModel)]="filters.hasCv">
            <option value="">Todos</option>
            <option value="yes">Con CV</option>
            <option value="no">Sin CV</option>
          </select>
        </div>
      </div>
      <div class="grid two">
        <div class="field">
          <label>Idiomas</label>
          <select name="languageValues" multiple [(ngModel)]="filters.languageValues">
            @for (language of languages; track language) {
              <option [value]="language">{{ language }}</option>
            }
          </select>
        </div>
        <div class="field">
          <label>Modo idiomas</label>
          <select name="languageMode" [(ngModel)]="filters.languageMode">
            <option value="ANY">Cualquiera</option>
            <option value="ALL">Todos</option>
          </select>
        </div>
        <div class="field">
          <label>Programas</label>
          <select name="programValues" multiple [(ngModel)]="filters.programValues">
            @for (program of programs; track program) {
              <option [value]="program">{{ program }}</option>
            }
          </select>
        </div>
        <div class="field">
          <label>Modo programas</label>
          <select name="programMode" [(ngModel)]="filters.programMode">
            <option value="ANY">Cualquiera</option>
            <option value="ALL">Todos</option>
          </select>
        </div>
      </div>
      <div class="toolbar">
        <button class="button" type="submit">Buscar</button>
        <button class="button secondary" type="button" (click)="clear.emit()">Limpiar</button>
      </div>
    </form>
  `,
})
export class SearchFiltersComponent {
  @Input({ required: true }) filters!: SearchFilters;
  @Output() readonly search = new EventEmitter<SearchFilters>();
  @Output() readonly clear = new EventEmitter<void>();

  constructor(private readonly catalogs: CatalogService) {}

  get languages(): string[] {
    return this.catalogs.activeNames('language');
  }

  get programs(): string[] {
    return this.catalogs.activeNames('program');
  }

  get activeChips(): Array<{ key: string; label: string }> {
    const chips: Array<{ key: string; label: string }> = [];

    if (this.filters.text.trim()) {
      chips.push({ key: 'text', label: `Texto: ${this.filters.text.trim()}` });
    }
    this.filters.statusValues.forEach((status) => {
      chips.push({ key: `status:${status}`, label: `Estado: ${this.statusLabel(status)}` });
    });
    if (this.filters.hasCv) {
      chips.push({
        key: 'hasCv',
        label: this.filters.hasCv === 'yes' ? 'CV: Con CV' : 'CV: Sin CV',
      });
    }
    this.filters.languageValues.forEach((language) => {
      chips.push({ key: `lang:${language}`, label: `Idioma: ${language}` });
    });
    if (this.filters.languageValues.length) {
      chips.push({
        key: 'languageMode',
        label: `Modo idiomas: ${this.filters.languageMode === 'ALL' ? 'Todos' : 'Cualquiera'}`,
      });
    }
    this.filters.programValues.forEach((program) => {
      chips.push({ key: `program:${program}`, label: `Programa: ${program}` });
    });
    if (this.filters.programValues.length) {
      chips.push({
        key: 'programMode',
        label: `Modo programas: ${this.filters.programMode === 'ALL' ? 'Todos' : 'Cualquiera'}`,
      });
    }

    return chips;
  }

  removeChip(key: string): void {
    if (key === 'text') {
      this.filters.text = '';
    } else if (key === 'hasCv') {
      this.filters.hasCv = '';
    } else if (key === 'languageMode') {
      this.filters.languageMode = 'ANY';
    } else if (key === 'programMode') {
      this.filters.programMode = 'ANY';
    } else if (key.startsWith('status:')) {
      const value = key.slice('status:'.length);
      this.filters.statusValues = this.filters.statusValues.filter((item) => item !== value);
    } else if (key.startsWith('lang:')) {
      const value = key.slice('lang:'.length);
      this.filters.languageValues = this.filters.languageValues.filter((item) => item !== value);
    } else if (key.startsWith('program:')) {
      const value = key.slice('program:'.length);
      this.filters.programValues = this.filters.programValues.filter((item) => item !== value);
    }

    this.search.emit(this.filters);
  }

  private statusLabel(status: string): string {
    switch (status) {
      case 'new':
        return 'Nuevo';
      case 'available':
        return 'Disponible';
      case 'in_process':
        return 'En proceso';
      case 'hired':
        return 'Contratado';
      case 'rejected':
        return 'Descartado';
      default:
        return status;
    }
  }
}
