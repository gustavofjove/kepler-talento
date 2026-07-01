import { Component } from '@angular/core';
import { SlicePipe } from '@angular/common';
import { RouterLink } from '@angular/router';
import { AuthService } from '../../../core/auth/auth.service';
import { CandidateService } from '../services/candidate.service';

@Component({
  selector: 'rrhh-candidate-list-page',
  standalone: true,
  imports: [RouterLink, SlicePipe],
  template: `
    <section class="page">
      <div class="toolbar">
        <div class="page-header">
          <h1>Candidatos</h1>
          <p class="muted">Alta, consulta, edicion y baja logica.</p>
        </div>
        @if (auth.hasPermission('create_candidates')) {
          <a class="button" routerLink="/app/candidates/new">Nuevo candidato</a>
        }
      </div>
      <div class="panel table-wrap">
        <table>
          <thead>
            <tr>
              <th>Nombre</th>
              <th>Telefono</th>
              <th>Estado</th>
              <th>CV</th>
              <th>Actualizado</th>
              <th></th>
            </tr>
          </thead>
          <tbody>
            @for (candidate of candidateService.list(); track candidate.id) {
              <tr>
                <td>
                  <strong>{{ candidate.firstName }} {{ candidate.lastName }}</strong>
                  <div class="muted">{{ candidate.email }}</div>
                </td>
                <td>{{ candidate.phone }}</td>
                <td>
                  <span class="badge">{{ candidate.status }}</span>
                </td>
                <td>{{ candidate.documents.length ? 'Disponible' : 'Pendiente' }}</td>
                <td>{{ candidate.updatedAt | slice: 0 : 10 }}</td>
                <td>
                  <a class="button secondary" [routerLink]="['/app/candidates', candidate.id]"
                    >Abrir</a
                  >
                </td>
              </tr>
            }
          </tbody>
        </table>
      </div>
    </section>
  `,
})
export class CandidateListPageComponent {
  constructor(
    readonly candidateService: CandidateService,
    readonly auth: AuthService,
  ) {}
}
