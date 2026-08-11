import { Component, Input } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { CatalogService } from '../../catalogs/services/catalog.service';
import { CandidateProgram } from '../models/candidate.models';
import { CandidateRelationsService } from '../services/candidate-relations.service';

@Component({
  selector: 'rrhh-candidate-programs',
  standalone: true,
  imports: [FormsModule],
  template: `
    <section class="section-block">
      <h3 class="section-title">Programas</h3>
      @if (!programs.length) {
        <p class="empty-state">Sin programas asociados.</p>
      }
      <div class="item-list">
        @for (item of programs; track item.id) {
          <div class="item-row">
            <p class="item-main">
              <span class="badge">{{ item.program }}</span> {{ item.level }}
              @if (item.yearsExperience !== undefined) {
                ({{ item.yearsExperience }} años)
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
              <label>Programa</label>
              <select name="program" [(ngModel)]="draft.program" required>
                <option value="" disabled>Selecciona</option>
                @for (option of programOptions; track option) {
                  <option [value]="option">{{ option }}</option>
                }
              </select>
            </div>
            <div class="field">
              <label>Nivel</label>
              <select name="level" [(ngModel)]="draft.level" required>
                <option value="" disabled>Selecciona</option>
                @for (option of levelOptions; track option) {
                  <option [value]="option">{{ option }}</option>
                }
              </select>
            </div>
            <div class="field">
              <label>Años de experiencia</label>
              <input
                name="yearsExperience"
                type="number"
                min="0"
                [(ngModel)]="draft.yearsExperience"
              />
            </div>
          </div>
          @if (error) {
            <p class="empty-state">{{ error }}</p>
          }
          <div class="form-actions">
            <button class="button" type="submit">Añadir programa</button>
          </div>
        </form>
      }
    </section>
  `,
})
export class CandidateProgramsComponent {
  @Input() candidateId = '';
  @Input() programs: CandidateProgram[] = [];
  @Input() canEdit = false;

  draft: { program: string; level: string; yearsExperience: number | undefined } = {
    program: '',
    level: '',
    yearsExperience: undefined,
  };
  error = '';

  constructor(
    private readonly relations: CandidateRelationsService,
    private readonly catalogs: CatalogService,
  ) {}

  get programOptions(): string[] {
    return this.catalogs.activeNames('program');
  }

  get levelOptions(): string[] {
    return this.catalogs.activeNames('program_level');
  }

  add(): void {
    try {
      this.relations.addProgram(this.candidateId, { ...this.draft });
      this.draft = { program: '', level: '', yearsExperience: undefined };
      this.error = '';
    } catch (err) {
      this.error = (err as Error).message;
    }
  }

  remove(programId: string): void {
    this.relations.removeProgram(this.candidateId, programId);
  }
}
