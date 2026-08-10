import { Component, Input } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { CatalogService } from '../../catalogs/services/catalog.service';
import { CandidateSkill } from '../models/candidate.models';
import { CandidateRelationsService } from '../services/candidate-relations.service';

@Component({
  selector: 'rrhh-candidate-skills',
  standalone: true,
  imports: [FormsModule],
  template: `
    <section class="section-block">
      <h3 class="section-title">Habilidades</h3>
      @if (!skills.length) {
        <p class="empty-state">Sin habilidades registradas.</p>
      }
      <div class="item-list">
        @for (item of skills; track item.id) {
          <div class="item-row">
            <p class="item-main">
              <span class="badge">{{ item.skill }}</span> {{ item.level }}
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
              <label>Habilidad</label>
              <select name="skill" [(ngModel)]="draft.skill" required>
                <option value="" disabled>Selecciona</option>
                @for (option of skillOptions; track option) {
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
          </div>
          @if (error) {
            <p class="empty-state">{{ error }}</p>
          }
          <div class="form-actions">
            <button class="button" type="submit">Anadir habilidad</button>
          </div>
        </form>
      }
    </section>
  `,
})
export class CandidateSkillsComponent {
  @Input() candidateId = '';
  @Input() skills: CandidateSkill[] = [];
  @Input() canEdit = false;

  draft = { skill: '', level: '' };
  error = '';

  constructor(
    private readonly relations: CandidateRelationsService,
    private readonly catalogs: CatalogService,
  ) {}

  get skillOptions(): string[] {
    return this.catalogs.activeNames('skill');
  }

  get levelOptions(): string[] {
    return this.catalogs.activeNames('skill_level');
  }

  add(): void {
    try {
      this.relations.addSkill(this.candidateId, { ...this.draft });
      this.draft = { skill: '', level: '' };
      this.error = '';
    } catch (err) {
      this.error = (err as Error).message;
    }
  }

  remove(skillId: string): void {
    this.relations.removeSkill(this.candidateId, skillId);
  }
}
