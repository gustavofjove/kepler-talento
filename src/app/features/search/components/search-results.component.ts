import { Component, Input } from '@angular/core';
import { SlicePipe } from '@angular/common';
import { RouterLink } from '@angular/router';
import { AuthService } from '../../../core/auth/auth.service';
import { ToastService } from '../../../core/services/toast.service';
import { DocumentService } from '../../documents/services/document.service';
import { SearchResult } from '../models/search.models';

@Component({
  selector: 'rrhh-search-results',
  standalone: true,
  imports: [RouterLink, SlicePipe],
  template: `
    <div class="section-block">
      <div class="table-wrap">
        <table>
          <thead>
            <tr>
              <th>Candidato</th>
              <th>Teléfono</th>
              <th>Estado</th>
              <th>CV</th>
              <th>Actualizado</th>
              <th></th>
            </tr>
          </thead>
          <tbody>
            @for (result of results; track result.candidateId) {
              <tr>
                <td>
                  <strong>{{ result.firstName }} {{ result.lastName }}</strong>
                  <div class="muted">{{ result.email }}</div>
                </td>
                <td>{{ result.phone }}</td>
                <td>
                  <span class="badge">{{ result.status }}</span>
                </td>
                <td>{{ result.hasPrimaryCv ? 'Disponible' : 'Pendiente' }}</td>
                <td>{{ result.updatedAt | slice: 0 : 10 }}</td>
                <td>
                  <div class="form-actions">
                    <a
                      class="button secondary"
                      [routerLink]="['/app/candidates', result.candidateId]"
                    >
                      Detalle
                    </a>
                    <button
                      class="button ghost"
                      type="button"
                      [disabled]="!canOpenCv(result)"
                      (click)="openCv(result)"
                    >
                      Abrir CV
                    </button>
                  </div>
                </td>
              </tr>
            } @empty {
              <tr>
                <td colspan="6" class="muted">Sin resultados.</td>
              </tr>
            }
          </tbody>
        </table>
      </div>
      @if (!auth.hasPermission('download_candidate_documents')) {
        <p class="empty-state">Tu rol no permite abrir CVs desde resultados.</p>
      }
    </div>
  `,
})
export class SearchResultsComponent {
  @Input() results: SearchResult[] = [];

  constructor(
    readonly auth: AuthService,
    private readonly documents: DocumentService,
    private readonly toast: ToastService,
  ) {}

  canOpenCv(result: SearchResult): boolean {
    return (
      this.auth.hasPermission('download_candidate_documents') &&
      Boolean(result.hasPrimaryCv && result.primaryCvDocumentId)
    );
  }

  openCv(result: SearchResult): void {
    if (!this.canOpenCv(result)) {
      this.toast.show('No hay CV principal disponible o no tienes permiso.', 'warning');
      return;
    }

    try {
      const secure = this.documents.createSecureUrl(
        result.candidateId,
        result.primaryCvDocumentId!,
      );
      window.open(secure.url, '_blank', 'noopener,noreferrer');
    } catch (error) {
      this.toast.show(error instanceof Error ? error.message : 'No se pudo abrir el CV.', 'error');
    }
  }
}
