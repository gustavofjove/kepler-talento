import { Component, Input } from '@angular/core';
import { FormsModule } from '@angular/forms';
import {
  DEFAULT_EDUCATION_STATUSES,
  DEFAULT_EDUCATION_TYPES,
} from '../../catalogs/models/catalog.models';
import { CandidateEducation } from '../models/candidate.models';
import { CandidateRelationsService } from '../services/candidate-relations.service';

@Component({
  selector: 'rrhh-candidate-education',
  standalone: true,
  imports: [FormsModule],
  template: `
    <section class="section-block">
      <h3 class="section-title">Formacion</h3>
      @if (!education.length) {
        <p class="empty-state">Sin formacion registrada.</p>
      }
      <div class="item-list">
        @for (item of education; track item.id) {
          <div class="item-row">
            <p class="item-main">
              <span class="badge">{{ item.degree }}</span> {{ item.institution }} ({{
                item.status
              }})
              @if (item.endYear) {
                · {{ item.endYear }}
              }
            </p>
            @if (canEdit) {
              <button class="button secondary" type="button" (click)="remove(item.id)">
                Quitar
              </button>
            }
          </div>
        }
      </div>
      @if (canEdit) {
        <form class="section-block" (ngSubmit)="add()">
          <div class="grid two">
            <div class="field">
              <label>Tipo</label>
              <select name="educationType" [(ngModel)]="draft.educationType" required>
                <option value="" disabled>Selecciona</option>
                @for (option of typeOptions; track option) {
                  <option [value]="option">{{ option }}</option>
                }
              </select>
            </div>
            <div class="field">
              <label>Titulacion</label>
              <input name="degree" [(ngModel)]="draft.degree" required />
            </div>
            <div class="field">
              <label>Especialidad</label>
              <input name="specialty" [(ngModel)]="draft.specialty" />
            </div>
            <div class="field">
              <label>Centro</label>
              <input name="institution" [(ngModel)]="draft.institution" />
            </div>
            <div class="field">
              <label>Ano de fin</label>
              <input name="endYear" type="number" [(ngModel)]="draft.endYear" />
            </div>
            <div class="field">
              <label>Estado</label>
              <select name="status" [(ngModel)]="draft.status" required>
                <option value="" disabled>Selecciona</option>
                @for (option of statusOptions; track option) {
                  <option [value]="option">{{ option }}</option>
                }
              </select>
            </div>
          </div>
          @if (error) {
            <p class="empty-state">{{ error }}</p>
          }
          <div class="form-actions">
            <button class="button" type="submit">Anadir formacion</button>
          </div>
        </form>
      }
    </section>
  `,
})
export class CandidateEducationComponent {
  @Input() candidateId = '';
  @Input() education: CandidateEducation[] = [];
  @Input() canEdit = false;

  readonly typeOptions = DEFAULT_EDUCATION_TYPES;
  readonly statusOptions = DEFAULT_EDUCATION_STATUSES;
  draft: {
    educationType: string;
    degree: string;
    specialty: string;
    institution: string;
    endYear: number | undefined;
    status: string;
  } = {
    educationType: '',
    degree: '',
    specialty: '',
    institution: '',
    endYear: undefined,
    status: '',
  };
  error = '';

  constructor(private readonly relations: CandidateRelationsService) {}

  add(): void {
    try {
      this.relations.addEducation(this.candidateId, { ...this.draft });
      this.draft = {
        educationType: '',
        degree: '',
        specialty: '',
        institution: '',
        endYear: undefined,
        status: '',
      };
      this.error = '';
    } catch (err) {
      this.error = (err as Error).message;
    }
  }

  remove(educationId: string): void {
    this.relations.removeEducation(this.candidateId, educationId);
  }
}
