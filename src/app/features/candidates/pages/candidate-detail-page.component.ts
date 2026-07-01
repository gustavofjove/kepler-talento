import { Component, Input } from '@angular/core';
import { SlicePipe } from '@angular/common';
import { RouterLink } from '@angular/router';
import { AuthService } from '../../../core/auth/auth.service';
import { CandidateDocumentsComponent } from '../components/candidate-documents.component';
import { CandidateEducationComponent } from '../components/candidate-education.component';
import { CandidateExperienceComponent } from '../components/candidate-experience.component';
import { CandidateLanguagesComponent } from '../components/candidate-languages.component';
import { CandidateProgramsComponent } from '../components/candidate-programs.component';
import { CandidateSkillsComponent } from '../components/candidate-skills.component';
import { CandidateService } from '../services/candidate.service';

@Component({
  selector: 'rrhh-candidate-detail-page',
  standalone: true,
  imports: [
    RouterLink,
    SlicePipe,
    CandidateLanguagesComponent,
    CandidateProgramsComponent,
    CandidateEducationComponent,
    CandidateExperienceComponent,
    CandidateSkillsComponent,
    CandidateDocumentsComponent,
  ],
  template: `
    @if (candidate; as item) {
      <section class="page">
        <div class="toolbar">
          <div class="page-header">
            <h1>{{ item.firstName }} {{ item.lastName }}</h1>
            <p class="muted">{{ item.email }} · {{ item.phone }}</p>
          </div>
          <div class="toolbar">
            @if (auth.hasPermission('edit_candidates')) {
              <a class="button secondary" [routerLink]="['/app/candidates', item.id, 'edit']"
                >Editar</a
              >
              <button class="button danger" type="button" (click)="deactivate(item.id)">
                Baja logica
              </button>
            }
          </div>
        </div>
        <div class="grid two">
          <article class="panel">
            <h2>Datos principales</h2>
            <p><strong>Estado:</strong> {{ item.status }}</p>
            <p><strong>Disponibilidad:</strong> {{ item.availability }}</p>
            <p><strong>Localidad:</strong> {{ item.location }} {{ item.province }}</p>
            <p><strong>Recepcion:</strong> {{ item.receivedAt || 'Pendiente' }}</p>
            <p><strong>Revision:</strong> {{ item.reviewDueAt || 'Pendiente' }}</p>
            <p>{{ item.notes }}</p>
          </article>
          <article class="panel">
            <h2>Auditoria</h2>
            <p><strong>Creado:</strong> {{ item.createdAt | slice: 0 : 19 }}</p>
            <p><strong>Actualizado:</strong> {{ item.updatedAt | slice: 0 : 19 }}</p>
            <p><strong>Activo:</strong> {{ item.isActive ? 'Si' : 'No' }}</p>
          </article>
        </div>
        <div class="grid two">
          <article class="panel">
            <rrhh-candidate-languages
              [candidateId]="item.id"
              [languages]="item.languages"
              [canEdit]="auth.hasPermission('edit_candidates')"
            />
          </article>
          <article class="panel">
            <rrhh-candidate-programs
              [candidateId]="item.id"
              [programs]="item.programs"
              [canEdit]="auth.hasPermission('edit_candidates')"
            />
          </article>
          <article class="panel">
            <rrhh-candidate-education
              [candidateId]="item.id"
              [education]="item.education"
              [canEdit]="auth.hasPermission('edit_candidates')"
            />
          </article>
          <article class="panel">
            <rrhh-candidate-experience
              [candidateId]="item.id"
              [experience]="item.experience"
              [canEdit]="auth.hasPermission('edit_candidates')"
            />
          </article>
          <article class="panel">
            <rrhh-candidate-skills
              [candidateId]="item.id"
              [skills]="item.skills"
              [canEdit]="auth.hasPermission('edit_candidates')"
            />
          </article>
          <article class="panel"><rrhh-candidate-documents [candidate]="item" /></article>
        </div>
      </section>
    } @else {
      <section class="panel">
        <h1>Candidato no encontrado</h1>
        <a class="button" routerLink="/app/candidates">Volver</a>
      </section>
    }
  `,
})
export class CandidateDetailPageComponent {
  @Input('id') candidateId = '';

  constructor(
    readonly auth: AuthService,
    private readonly candidateService: CandidateService,
  ) {}

  get candidate() {
    return this.candidateService.find(this.candidateId);
  }

  deactivate(id: string): void {
    this.candidateService.deactivate(id);
  }
}
