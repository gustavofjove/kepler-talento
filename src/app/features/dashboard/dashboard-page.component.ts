import { Component } from '@angular/core';
import { RouterLink } from '@angular/router';
import { CandidateService } from '../candidates/services/candidate.service';

@Component({
  selector: 'rrhh-dashboard-page',
  standalone: true,
  imports: [RouterLink],
  template: `
    <section class="page">
      <div class="toolbar">
        <div class="page-header">
          <h1>Dashboard</h1>
          <p class="muted">Resumen operativo de candidatos y CVs.</p>
        </div>
        <a class="button" routerLink="/app/candidates/new">Alta de candidato</a>
      </div>
      <div class="grid four">
        <article class="panel kpi-card">
          <span class="kpi-label">Candidatos activos</span>
          <span class="kpi-value">{{ activeCount }}</span>
        </article>
        <article class="panel kpi-card">
          <span class="kpi-label">Sin CV adjunto</span>
          <span class="kpi-value">{{ withoutCv }}</span>
        </article>
        <article class="panel kpi-card">
          <span class="kpi-label">Pendientes de revision</span>
          <span class="kpi-value">{{ pendingReview }}</span>
        </article>
        <article class="panel kpi-card">
          <span class="kpi-label">Recibidos este mes</span>
          <span class="kpi-value">{{ receivedThisMonth }}</span>
        </article>
      </div>
    </section>
  `,
})
export class DashboardPageComponent {
  private readonly today = new Date().toISOString().slice(0, 10);
  private readonly month = this.today.slice(0, 7);

  constructor(private readonly candidateService: CandidateService) {}

  get activeCount(): number {
    return this.candidateService.list().length;
  }

  get withoutCv(): number {
    return this.candidateService.list().filter((candidate) => candidate.documents.length === 0)
      .length;
  }

  get pendingReview(): number {
    return this.candidateService
      .list()
      .filter((candidate) => candidate.reviewDueAt && candidate.reviewDueAt < this.today).length;
  }

  get receivedThisMonth(): number {
    return this.candidateService
      .list()
      .filter((candidate) => candidate.receivedAt.startsWith(this.month)).length;
  }
}
