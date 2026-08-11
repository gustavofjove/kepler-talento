import { Component } from '@angular/core';
import { SlicePipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { AuthService } from '../../../core/auth/auth.service';
import { ToastService } from '../../../core/services/toast.service';
import { Candidate, CandidateStatus } from '../models/candidate.models';
import { CandidateService } from '../services/candidate.service';
import { ConfirmDialogService } from '../../../shared/components/confirm-dialog.service';

type SortField = 'updatedAt' | 'lastName' | 'status';
type HasCvFilter = '' | 'yes' | 'no';

@Component({
  selector: 'rrhh-candidate-list-page',
  standalone: true,
  imports: [RouterLink, SlicePipe, FormsModule],
  styles: [
    `
      /* One row keeps the filter bar short: text, the two short selects, the
         checkbox column matching their width, and the action pinned right. */
      .filters-bar {
        align-items: end;
        display: grid;
        gap: 10px 14px;
        grid-template-columns: minmax(200px, 2fr) 1fr 1fr 1fr auto;
        padding: 12px 16px;
      }
      /* Checkbox and button sit on the same baseline as the inputs beside them. */
      .filters-bar .inline-check,
      .filters-bar .filters-actions {
        min-height: 34px;
      }
      .filters-bar .inline-check {
        font-size: 13px;
      }
      .filters-bar .filters-actions {
        align-items: center;
        display: flex;
        justify-self: end;
      }
      .inactive-badge {
        margin-left: 6px;
        opacity: 0.75;
      }
      .results-footer {
        justify-content: space-between;
      }
      .page-size {
        align-items: center;
        display: flex;
        gap: 8px;
      }
      .page-size label {
        color: var(--fg-2);
        font-size: 11px;
        font-weight: 600;
        white-space: nowrap;
      }
      /* Outside a .field wrapper, so it needs the shared control look spelled out. */
      .page-size select {
        background: var(--bg-1);
        border: 1px solid var(--border);
        border-radius: var(--r-md);
        min-height: 34px;
        padding: 6px 10px;
        width: auto;
      }
      .pager {
        align-items: center;
        display: flex;
        gap: 12px;
      }
      @media (max-width: 900px) {
        .filters-bar {
          grid-template-columns: repeat(2, minmax(0, 1fr));
        }
      }
      @media (max-width: 560px) {
        .filters-bar {
          grid-template-columns: 1fr;
        }
      }
    `,
  ],
  template: `
    <section class="page">
      <div class="toolbar">
        <div class="page-header">
          <h1>Candidatos</h1>
          <p class="muted">Alta, consulta, edición y baja lógica.</p>
        </div>
        @if (auth.hasPermission('create_candidates')) {
          <a class="button" routerLink="/app/candidates/new">Nuevo candidato</a>
        }
      </div>

      <div class="panel filters-bar">
        <div class="field">
          <label for="filter-text">Texto</label>
          <input
            id="filter-text"
            name="text"
            [(ngModel)]="textFilter"
            placeholder="Nombre, email, teléfono"
            (ngModelChange)="goToPage(1)"
          />
        </div>
        <div class="field">
          <label for="filter-status">Estado</label>
          <select
            id="filter-status"
            name="status"
            [(ngModel)]="statusFilter"
            (ngModelChange)="goToPage(1)"
          >
            <option value="">Todos</option>
            @for (status of statusOptions; track status) {
              <option [value]="status">{{ status }}</option>
            }
          </select>
        </div>
        <div class="field">
          <label for="filter-hasCv">CV</label>
          <select
            id="filter-hasCv"
            name="hasCv"
            [(ngModel)]="hasCvFilter"
            (ngModelChange)="goToPage(1)"
          >
            <option value="">Todos</option>
            <option value="yes">Con CV</option>
            <option value="no">Sin CV</option>
          </select>
        </div>
        <label class="inline-check">
          <input
            name="includeInactive"
            type="checkbox"
            [(ngModel)]="includeInactive"
            (ngModelChange)="goToPage(1)"
          />
          Incluir inactivos
        </label>
        <div class="filters-actions">
          <button class="button secondary" type="button" (click)="clearFilters()">Limpiar</button>
        </div>
      </div>

      @if (activeFilterChips.length) {
        <div class="panel">
          <div class="toolbar">
            <strong>Filtros activos</strong>
            <div class="form-actions">
              @for (chip of activeFilterChips; track chip.key) {
                <button class="button ghost" type="button" (click)="removeFilter(chip.key)">
                  {{ chip.label }} ×
                </button>
              }
            </div>
          </div>
        </div>
      }

      <div class="toolbar">
        <p class="muted">
          Mostrando {{ pagedCandidates.length }} de {{ filteredCandidates.length }} candidatos
          @if (!includeInactive) {
            · Inactivos ocultos
          }
        </p>
        @if (!auth.hasPermission('edit_candidates')) {
          <p class="empty-state">
            Modo solo lectura: puedes consultar perfiles pero no modificar candidatos.
          </p>
        }
        @if (auth.hasPermission('edit_candidates')) {
          <div class="form-actions">
            <button
              class="button danger"
              type="button"
              [disabled]="selectedIds.size === 0"
              (click)="bulkDeactivate()"
            >
              Baja lógica masiva ({{ selectedIds.size }})
            </button>
            <button
              class="button secondary"
              type="button"
              [disabled]="selectedIds.size === 0"
              (click)="bulkReactivate()"
            >
              Alta lógica masiva ({{ selectedIds.size }})
            </button>
          </div>
        }
      </div>

      @if (!filteredCandidates.length) {
        <div class="empty-state">
          No hay candidatos con los filtros actuales. Ajusta filtros o crea un nuevo candidato.
        </div>
      }

      <div class="panel table-wrap">
        <table>
          <thead>
            <tr>
              @if (auth.hasPermission('edit_candidates')) {
                <th>
                  <input
                    type="checkbox"
                    [checked]="allVisibleSelected"
                    (change)="toggleSelectAll($any($event.target).checked)"
                  />
                </th>
              }
              <th>
                <button class="button ghost" type="button" (click)="toggleSort('lastName')">
                  Nombre {{ sortIndicator('lastName') }}
                </button>
              </th>
              <th>Teléfono</th>
              <th>
                <button class="button ghost" type="button" (click)="toggleSort('status')">
                  Estado {{ sortIndicator('status') }}
                </button>
              </th>
              <th>CV</th>
              <th>
                <button class="button ghost" type="button" (click)="toggleSort('updatedAt')">
                  Actualizado {{ sortIndicator('updatedAt') }}
                </button>
              </th>
              <th></th>
            </tr>
          </thead>
          <tbody>
            @for (candidate of pagedCandidates; track candidate.id) {
              <tr>
                @if (auth.hasPermission('edit_candidates')) {
                  <td>
                    <input
                      type="checkbox"
                      [checked]="isSelected(candidate.id)"
                      (change)="toggleSelected(candidate.id, $any($event.target).checked)"
                    />
                  </td>
                }
                <td>
                  <strong>{{ candidate.firstName }} {{ candidate.lastName }}</strong>
                  <div class="muted">{{ candidate.email }}</div>
                </td>
                <td>{{ candidate.phone }}</td>
                <td>
                  <span class="badge">{{ candidate.status }}</span>
                  @if (!candidate.isActive) {
                    <span class="badge inactive-badge">Inactivo</span>
                  }
                </td>
                <td>{{ candidate.documents.length ? 'Disponible' : 'Pendiente' }}</td>
                <td>{{ candidate.updatedAt | slice: 0 : 10 }}</td>
                <td>
                  <a class="button secondary" [routerLink]="['/app/candidates', candidate.id]"
                    >Abrir</a
                  >
                </td>
              </tr>
            } @empty {
              <tr>
                <td [attr.colspan]="auth.hasPermission('edit_candidates') ? 7 : 6" class="muted">
                  Sin candidatos para mostrar.
                </td>
              </tr>
            }
          </tbody>
        </table>
      </div>

      <div class="toolbar results-footer">
        <div class="page-size">
          <label for="page-size">Registros por página</label>
          <select
            id="page-size"
            name="pageSize"
            [(ngModel)]="pageSize"
            (ngModelChange)="goToPage(1)"
          >
            <option [ngValue]="10">10</option>
            <option [ngValue]="20">20</option>
            <option [ngValue]="50">50</option>
          </select>
        </div>
        @if (totalPages > 1) {
          <div class="pager">
            <button
              class="button secondary"
              type="button"
              [disabled]="page <= 1"
              (click)="goToPage(page - 1)"
            >
              Anterior
            </button>
            <span class="muted">Página {{ page }} de {{ totalPages }}</span>
            <button
              class="button secondary"
              type="button"
              [disabled]="page >= totalPages"
              (click)="goToPage(page + 1)"
            >
              Siguiente
            </button>
          </div>
        }
      </div>
    </section>
  `,
})
export class CandidateListPageComponent {
  readonly statusOptions: CandidateStatus[] = [
    'new',
    'available',
    'in_process',
    'hired',
    'rejected',
  ];

  textFilter = '';
  statusFilter: CandidateStatus | '' = '';
  hasCvFilter: HasCvFilter = '';
  includeInactive = false;

  sortField: SortField = 'updatedAt';
  sortDirection: 'asc' | 'desc' = 'desc';

  page = 1;
  pageSize = 10;
  readonly selectedIds = new Set<string>();

  constructor(
    readonly candidateService: CandidateService,
    readonly auth: AuthService,
    private readonly toast: ToastService,
    private readonly confirmDialog: ConfirmDialogService,
  ) {}

  get filteredCandidates(): Candidate[] {
    const text = this.textFilter.trim().toLowerCase();
    const candidates = this.candidateService.list(this.includeInactive).filter((candidate) => {
      const textMatch =
        !text ||
        [candidate.firstName, candidate.lastName, candidate.email, candidate.phone]
          .join(' ')
          .toLowerCase()
          .includes(text);
      const statusMatch = !this.statusFilter || candidate.status === this.statusFilter;
      const hasCv = candidate.documents.length > 0;
      const cvMatch = !this.hasCvFilter || (this.hasCvFilter === 'yes' ? hasCv : !hasCv);
      return textMatch && statusMatch && cvMatch;
    });

    return candidates.slice().sort((a, b) => this.compareCandidates(a, b));
  }

  get totalPages(): number {
    return Math.max(Math.ceil(this.filteredCandidates.length / this.pageSize), 1);
  }

  get pagedCandidates(): Candidate[] {
    const validPage = Math.min(Math.max(this.page, 1), this.totalPages);
    const start = (validPage - 1) * this.pageSize;
    const end = start + this.pageSize;
    return this.filteredCandidates.slice(start, end);
  }

  get allVisibleSelected(): boolean {
    return (
      this.pagedCandidates.length > 0 &&
      this.pagedCandidates.every((item) => this.selectedIds.has(item.id))
    );
  }

  get activeFilterChips(): Array<{ key: string; label: string }> {
    const chips: Array<{ key: string; label: string }> = [];
    if (this.textFilter.trim()) {
      chips.push({ key: 'text', label: `Texto: ${this.textFilter.trim()}` });
    }
    if (this.statusFilter) {
      chips.push({ key: 'status', label: `Estado: ${this.statusFilter}` });
    }
    if (this.hasCvFilter) {
      chips.push({ key: 'hasCv', label: this.hasCvFilter === 'yes' ? 'CV: Con CV' : 'CV: Sin CV' });
    }
    if (this.includeInactive) {
      chips.push({ key: 'includeInactive', label: 'Incluye inactivos' });
    }
    return chips;
  }

  clearFilters(): void {
    this.textFilter = '';
    this.statusFilter = '';
    this.hasCvFilter = '';
    this.includeInactive = false;
    this.page = 1;
  }

  removeFilter(key: string): void {
    if (key === 'text') {
      this.textFilter = '';
    } else if (key === 'status') {
      this.statusFilter = '';
    } else if (key === 'hasCv') {
      this.hasCvFilter = '';
    } else if (key === 'includeInactive') {
      this.includeInactive = false;
    }
    this.goToPage(1);
  }

  toggleSort(field: SortField): void {
    if (this.sortField === field) {
      this.sortDirection = this.sortDirection === 'asc' ? 'desc' : 'asc';
    } else {
      this.sortField = field;
      this.sortDirection = field === 'updatedAt' ? 'desc' : 'asc';
    }
    this.goToPage(1);
  }

  sortIndicator(field: SortField): string {
    if (this.sortField !== field) {
      return '';
    }
    return this.sortDirection === 'asc' ? '↑' : '↓';
  }

  goToPage(page: number): void {
    this.page = Math.min(Math.max(page, 1), this.totalPages);
  }

  isSelected(candidateId: string): boolean {
    return this.selectedIds.has(candidateId);
  }

  toggleSelected(candidateId: string, checked: boolean): void {
    if (checked) {
      this.selectedIds.add(candidateId);
    } else {
      this.selectedIds.delete(candidateId);
    }
  }

  toggleSelectAll(checked: boolean): void {
    if (checked) {
      this.pagedCandidates.forEach((candidate) => this.selectedIds.add(candidate.id));
      return;
    }
    this.pagedCandidates.forEach((candidate) => this.selectedIds.delete(candidate.id));
  }

  async bulkDeactivate(): Promise<void> {
    if (!this.selectedIds.size) {
      this.toast.show('Selecciona al menos un candidato para aplicar baja lógica.', 'warning');
      return;
    }

    const selected = this.candidateService
      .list(true)
      .filter((candidate) => this.selectedIds.has(candidate.id));

    if (!selected.length) {
      this.selectedIds.clear();
      this.toast.show('No hay candidatos válidos seleccionados.', 'warning');
      return;
    }

    const confirmation = await this.confirmDialog.confirm({
      title: 'Confirmar baja lógica masiva',
      message: `Se aplicará baja lógica a ${selected.length} candidato(s).`,
      confirmText: 'Aplicar baja',
      cancelText: 'Cancelar',
      danger: true,
    });
    if (!confirmation) {
      return;
    }

    const updated = this.candidateService.deactivateMany(selected.map((candidate) => candidate.id));
    this.selectedIds.clear();
    this.toast.show(`Baja lógica aplicada a ${updated} candidato(s).`, 'success');
    this.goToPage(1);
  }

  async bulkReactivate(): Promise<void> {
    if (!this.selectedIds.size) {
      this.toast.show('Selecciona al menos un candidato para aplicar alta lógica.', 'warning');
      return;
    }

    const selected = this.candidateService
      .list(true)
      .filter((candidate) => this.selectedIds.has(candidate.id));

    if (!selected.length) {
      this.selectedIds.clear();
      this.toast.show('No hay candidatos válidos seleccionados.', 'warning');
      return;
    }

    const confirmation = await this.confirmDialog.confirm({
      title: 'Confirmar alta lógica masiva',
      message: `Se aplicará alta lógica a ${selected.length} candidato(s).`,
      confirmText: 'Aplicar alta',
      cancelText: 'Cancelar',
    });
    if (!confirmation) {
      return;
    }

    const updated = this.candidateService.reactivateMany(selected.map((candidate) => candidate.id));
    this.selectedIds.clear();
    this.toast.show(`Alta lógica aplicada a ${updated} candidato(s).`, 'success');
    this.goToPage(1);
  }

  private compareCandidates(a: Candidate, b: Candidate): number {
    let value = 0;
    if (this.sortField === 'updatedAt') {
      value = a.updatedAt.localeCompare(b.updatedAt);
    } else if (this.sortField === 'lastName') {
      value = `${a.lastName} ${a.firstName}`.localeCompare(`${b.lastName} ${b.firstName}`);
    } else {
      value = a.status.localeCompare(b.status);
    }
    return this.sortDirection === 'asc' ? value : -value;
  }
}
