import { Component, Input } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { CatalogService } from '../../catalogs/services/catalog.service';
import { CandidateLanguage } from '../models/candidate.models';
import { CandidateRelationsService } from '../services/candidate-relations.service';

@Component({
  selector: 'rrhh-candidate-languages',
  standalone: true,
  imports: [FormsModule],
  template: `
    <section class="section-block">
      <h3 class="section-title">Idiomas</h3>
      @if (!languages.length) {
        <p class="empty-state">Sin idiomas asociados.</p>
      }
      <div class="item-list">
        @for (item of languages; track item.id) {
          <div class="item-row">
            <p class="item-main">
              <span class="badge">{{ item.language }}</span> {{ item.level }}
              {{ item.certification || '' }}
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
              <label>Idioma</label>
              <select name="language" [(ngModel)]="draft.language" required>
                <option value="" disabled>Selecciona</option>
                @for (option of languageOptions; track option) {
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
              <label>Certificacion</label>
              <input name="certification" [(ngModel)]="draft.certification" />
            </div>
          </div>
          @if (error) {
            <p class="empty-state">{{ error }}</p>
          }
          <div class="form-actions">
            <button class="button" type="submit">Anadir idioma</button>
          </div>
        </form>
      }
    </section>
  `,
})
export class CandidateLanguagesComponent {
  @Input() candidateId = '';
  @Input() languages: CandidateLanguage[] = [];
  @Input() canEdit = false;

  draft = { language: '', level: '', certification: '' };
  error = '';

  constructor(
    private readonly relations: CandidateRelationsService,
    private readonly catalogs: CatalogService,
  ) {}

  get languageOptions(): string[] {
    return this.catalogs.activeNames('language');
  }

  get levelOptions(): string[] {
    return this.catalogs.activeNames('language_level');
  }

  add(): void {
    try {
      this.relations.addLanguage(this.candidateId, { ...this.draft });
      this.draft = { language: '', level: '', certification: '' };
      this.error = '';
    } catch (err) {
      this.error = (err as Error).message;
    }
  }

  remove(languageId: string): void {
    this.relations.removeLanguage(this.candidateId, languageId);
  }
}
