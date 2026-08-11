import { Component, Input } from '@angular/core';
import { Router } from '@angular/router';
import { CandidateFormComponent } from '../components/candidate-form.component';
import { CandidateDraft } from '../models/candidate.models';
import { CandidateService } from '../services/candidate.service';

@Component({
  selector: 'rrhh-candidate-edit-page',
  standalone: true,
  imports: [CandidateFormComponent],
  template: `
    <section class="page">
      <div class="page-header">
        <h1>{{ candidateId ? 'Editar candidato' : 'Alta de candidato' }}</h1>
        <p class="muted">Los cambios quedan preparados para auditoría y RLS.</p>
      </div>
      <div class="panel">
        <rrhh-candidate-form [candidate]="candidate" (save)="save($event)" />
      </div>
    </section>
  `,
})
export class CandidateEditPageComponent {
  @Input('id') candidateId = '';

  constructor(
    private readonly candidateService: CandidateService,
    private readonly router: Router,
  ) {}

  get candidate() {
    return this.candidateId ? this.candidateService.find(this.candidateId) : undefined;
  }

  async save(draft: CandidateDraft): Promise<void> {
    const candidate = this.candidateId
      ? this.candidateService.update(this.candidateId, draft)
      : this.candidateService.create(draft);
    await this.router.navigate(['/app/candidates', candidate.id]);
  }
}
