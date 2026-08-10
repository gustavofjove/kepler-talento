import { Component, Input } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { CatalogService } from '../../catalogs/services/catalog.service';
import { CandidateExperience } from '../models/candidate.models';
import { CandidateRelationsService } from '../services/candidate-relations.service';

@Component({
  selector: 'rrhh-candidate-experience',
  standalone: true,
  imports: [FormsModule],
  template: `
    <section class="section-block">
      <h3 class="section-title">Experiencia</h3>
      @if (!experience.length) {
        <p class="empty-state">Sin experiencia registrada.</p>
      }
      <div class="item-list">
        @for (item of experience; track item.id) {
          <div class="item-row">
            <p class="item-main">
              <span class="badge">{{ item.position }}</span> {{ item.company }} ({{ item.sector }})
              @if (item.isCurrent) {
                · Actual
              } @else if (item.yearsExperience !== undefined) {
                · {{ item.yearsExperience }} anos
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
              <label>Empresa</label>
              <input name="company" [(ngModel)]="draft.company" required />
            </div>
            <div class="field">
              <label>Puesto</label>
              <input name="position" [(ngModel)]="draft.position" required />
            </div>
            <div class="field">
              <label>Sector</label>
              <select name="sector" [(ngModel)]="draft.sector" required>
                <option value="" disabled>Selecciona</option>
                @for (option of sectorOptions; track option) {
                  <option [value]="option">{{ option }}</option>
                }
              </select>
            </div>
            <div class="field">
              <label>Fecha inicio</label>
              <input name="startDate" type="date" [(ngModel)]="draft.startDate" />
            </div>
            <div class="field">
              <label>Fecha fin</label>
              <input
                name="endDate"
                type="date"
                [(ngModel)]="draft.endDate"
                [disabled]="draft.isCurrent"
              />
            </div>
            <div class="field">
              <label>Anos de experiencia</label>
              <input
                name="yearsExperience"
                type="number"
                min="0"
                [(ngModel)]="draft.yearsExperience"
              />
            </div>
            <div class="field">
              <label class="inline-check">
                <input name="isCurrent" type="checkbox" [(ngModel)]="draft.isCurrent" />
                Puesto actual
              </label>
            </div>
          </div>
          @if (error) {
            <p class="empty-state">{{ error }}</p>
          }
          <div class="form-actions">
            <button class="button" type="submit">Anadir experiencia</button>
          </div>
        </form>
      }
    </section>
  `,
})
export class CandidateExperienceComponent {
  @Input() candidateId = '';
  @Input() experience: CandidateExperience[] = [];
  @Input() canEdit = false;

  draft: {
    company: string;
    position: string;
    sector: string;
    startDate: string;
    endDate: string;
    yearsExperience: number | undefined;
    isCurrent: boolean;
  } = {
    company: '',
    position: '',
    sector: '',
    startDate: '',
    endDate: '',
    yearsExperience: undefined,
    isCurrent: false,
  };
  error = '';

  constructor(
    private readonly relations: CandidateRelationsService,
    private readonly catalogs: CatalogService,
  ) {}

  get sectorOptions(): string[] {
    return this.catalogs.activeNames('sector');
  }

  add(): void {
    try {
      this.relations.addExperience(this.candidateId, { ...this.draft });
      this.draft = {
        company: '',
        position: '',
        sector: '',
        startDate: '',
        endDate: '',
        yearsExperience: undefined,
        isCurrent: false,
      };
      this.error = '';
    } catch (err) {
      this.error = (err as Error).message;
    }
  }

  remove(experienceId: string): void {
    this.relations.removeExperience(this.candidateId, experienceId);
  }
}
