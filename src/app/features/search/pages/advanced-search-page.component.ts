import { Component } from '@angular/core';
import { AuthService } from '../../../core/auth/auth.service';
import { ToastService } from '../../../core/services/toast.service';
import { CandidateSearchService } from '../services/candidate-search.service';
import { SearchFilters, SearchResult } from '../models/search.models';
import { SearchFiltersComponent } from '../components/search-filters.component';
import { SearchResultsComponent } from '../components/search-results.component';

@Component({
  selector: 'rrhh-advanced-search-page',
  standalone: true,
  imports: [SearchFiltersComponent, SearchResultsComponent],
  template: `
    <section class="page">
      <div class="toolbar">
        <div class="page-header">
          <h1>Busqueda avanzada</h1>
          <p class="muted">Filtros combinados, ANY/ALL y resultados sin duplicados.</p>
        </div>
        <button
          class="button secondary"
          type="button"
          [disabled]="!auth.hasPermission('export_candidates')"
          (click)="export()"
        >
          Exportar CSV
        </button>
      </div>
      <div class="panel">
        <rrhh-search-filters [filters]="filters" (search)="run($event)" (clear)="clear()" />
      </div>
      <div class="panel">
        <rrhh-search-results [results]="results" />
      </div>
    </section>
  `,
})
export class AdvancedSearchPageComponent {
  filters = this.searchService.emptyFilters();
  results: SearchResult[] = this.searchService.search(this.filters);

  constructor(
    readonly auth: AuthService,
    private readonly searchService: CandidateSearchService,
    private readonly toast: ToastService,
  ) {}

  run(filters: SearchFilters): void {
    this.results = this.searchService.search(filters);
  }

  clear(): void {
    this.filters = this.searchService.emptyFilters();
    this.results = this.searchService.search(this.filters);
  }

  export(): void {
    const rows = this.results.map((item) => ({
      nombre: item.firstName,
      apellidos: item.lastName,
      telefono: item.phone,
      email: item.email,
      estado: item.status,
      cv_disponible: item.hasPrimaryCv ? 'si' : 'no',
    }));
    const csv = [
      Object.keys(
        rows[0] ?? {
          nombre: '',
          apellidos: '',
          telefono: '',
          email: '',
          estado: '',
          cv_disponible: '',
        },
      ).join(','),
      ...rows.map((row) =>
        Object.values(row)
          .map((value) => `"${String(value).replace(/"/g, '""')}"`)
          .join(','),
      ),
    ].join('\n');
    const url = URL.createObjectURL(new Blob([csv], { type: 'text/csv' }));
    const link = document.createElement('a');
    link.href = url;
    link.download = 'candidatos.csv';
    link.click();
    URL.revokeObjectURL(url);
    this.toast.show('Exportacion generada sin rutas internas de Storage.', 'success');
  }
}
