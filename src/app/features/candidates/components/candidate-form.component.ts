import { Component, EventEmitter, Input, Output } from '@angular/core';
import { FormsModule } from '@angular/forms';
import {
  Candidate,
  CandidateDraft,
  CandidateStatus,
  EMPTY_CANDIDATE_DRAFT,
} from '../models/candidate.models';

@Component({
  selector: 'rrhh-candidate-form',
  standalone: true,
  imports: [FormsModule],
  template: `
    <form class="section-block" (ngSubmit)="submit()">
      <div class="grid two">
        <div class="field">
          <label>Nombre</label>
          <input name="firstName" [(ngModel)]="draft.firstName" required />
        </div>
        <div class="field">
          <label>Apellidos</label>
          <input name="lastName" [(ngModel)]="draft.lastName" required />
        </div>
        <div class="field">
          <label>Teléfono</label>
          <input name="phone" [(ngModel)]="draft.phone" />
        </div>
        <div class="field">
          <label>Email</label>
          <input name="email" type="email" [(ngModel)]="draft.email" />
        </div>
        <div class="field">
          <label>Localidad</label>
          <input name="location" [(ngModel)]="draft.location" />
        </div>
        <div class="field">
          <label>Provincia</label>
          <input name="province" [(ngModel)]="draft.province" />
        </div>
        <div class="field">
          <label>Disponibilidad</label>
          <input name="availability" [(ngModel)]="draft.availability" />
        </div>
        <div class="field">
          <label>Estado</label>
          <select name="status" [(ngModel)]="draft.status">
            <option value="new">Nuevo</option>
            <option value="available">Disponible</option>
            <option value="in_process">En proceso</option>
            <option value="hired">Contratado</option>
            <option value="rejected">Descartado</option>
          </select>
        </div>
        <div class="field">
          <label>Fecha recepción</label>
          <input name="receivedAt" type="date" [(ngModel)]="draft.receivedAt" />
        </div>
        <div class="field">
          <label>Fecha revisión</label>
          <input name="reviewDueAt" type="date" [(ngModel)]="draft.reviewDueAt" />
        </div>
      </div>
      <div class="field">
        <label>Observaciones internas</label>
        <textarea name="notes" rows="4" [(ngModel)]="draft.notes"></textarea>
      </div>
      @if (error) {
        <p class="empty-state">{{ error }}</p>
      }
      <div class="form-actions">
        <button class="button" type="submit">Guardar</button>
      </div>
    </form>
  `,
})
export class CandidateFormComponent {
  @Output() readonly save = new EventEmitter<CandidateDraft>();
  draft: CandidateDraft = structuredClone(EMPTY_CANDIDATE_DRAFT);
  error = '';

  @Input() set candidate(value: Candidate | undefined) {
    if (!value) {
      this.draft = structuredClone(EMPTY_CANDIDATE_DRAFT);
      return;
    }
    const {
      id: _id,
      createdAt: _createdAt,
      updatedAt: _updatedAt,
      languages: _languages,
      programs: _programs,
      education: _education,
      experience: _experience,
      skills: _skills,
      documents: _documents,
      ...draft
    } = value;
    this.draft = { ...draft, status: draft.status as CandidateStatus };
  }

  submit(): void {
    if (!this.draft.firstName.trim() || !this.draft.lastName.trim()) {
      this.error = 'Nombre y apellidos son obligatorios.';
      return;
    }
    this.error = '';
    this.save.emit({ ...this.draft });
  }
}
