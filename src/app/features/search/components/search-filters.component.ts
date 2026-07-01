import { Component, EventEmitter, Input, Output } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { DEFAULT_LANGUAGES, DEFAULT_PROGRAMS } from '../../catalogs/models/catalog.models';
import { SearchFilters } from '../models/search.models';

@Component({
  selector: 'rrhh-search-filters',
  standalone: true,
  imports: [FormsModule],
  template: `
    <form class="grid" (ngSubmit)="search.emit(filters)">
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
  readonly languages = DEFAULT_LANGUAGES;
  readonly programs = DEFAULT_PROGRAMS;
}
