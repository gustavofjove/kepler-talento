import { Component, Input } from '@angular/core';
import { SlicePipe } from '@angular/common';
import { RouterLink } from '@angular/router';
import { AuthService } from '../../../core/auth/auth.service';
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
              <th>Telefono</th>
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
                  <a
                    class="button secondary"
                    [routerLink]="['/app/candidates', result.candidateId]"
                  >
                    Detalle
                  </a>
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

  constructor(readonly auth: AuthService) {}
}
