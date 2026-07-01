import { Component, Input } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { AuthService } from '../../../core/auth/auth.service';
import { DocumentService } from '../../documents/services/document.service';
import { Candidate } from '../models/candidate.models';

@Component({
  selector: 'rrhh-candidate-documents',
  standalone: true,
  imports: [FormsModule],
  template: `
    <section class="section-block">
      <h3 class="section-title">Documentos</h3>
      @if (!candidate?.documents?.length) {
        <p class="empty-state">Sin CV adjunto.</p>
      }
      <div class="item-list">
        @for (document of candidate?.documents ?? []; track document.id) {
          <div class="item-row">
            <p class="item-main">
              <strong>{{ document.originalFilename }}</strong>
              <span class="badge">{{
                document.isPrimary ? 'Principal' : document.documentType
              }}</span>
            </p>
            <button
              class="button secondary"
              type="button"
              [disabled]="!auth.hasPermission('download_candidate_documents')"
              (click)="open(document.id)"
            >
              Abrir seguro
            </button>
          </div>
        }
      </div>
      @if (auth.hasPermission('upload_candidate_documents')) {
        <form class="section-block" (ngSubmit)="upload()">
          <div class="grid two">
            <div class="field">
              <label>Archivo CV (PDF)</label>
              <input
                name="file"
                type="file"
                accept="application/pdf"
                (change)="onFileSelected($event)"
              />
            </div>
            <div class="field">
              <label class="inline-check">
                <input name="isPrimary" type="checkbox" [(ngModel)]="isPrimary" />
                Marcar como CV principal
              </label>
            </div>
          </div>
          @if (error) {
            <p class="empty-state">{{ error }}</p>
          }
          <div class="form-actions">
            <button class="button" type="submit" [disabled]="!selectedFile">Subir CV</button>
          </div>
        </form>
      }
    </section>
  `,
})
export class CandidateDocumentsComponent {
  @Input() candidate: Candidate | undefined;

  selectedFile: File | undefined;
  isPrimary = true;
  error = '';

  constructor(
    readonly auth: AuthService,
    private readonly documentService: DocumentService,
  ) {}

  onFileSelected(event: Event): void {
    const input = event.target as HTMLInputElement;
    this.selectedFile = input.files?.[0];
  }

  upload(): void {
    if (!this.candidate || !this.selectedFile) {
      return;
    }
    try {
      this.documentService.upload({
        candidateId: this.candidate.id,
        file: this.selectedFile,
        isPrimary: this.isPrimary,
      });
      this.selectedFile = undefined;
      this.isPrimary = true;
      this.error = '';
    } catch (err) {
      this.error = (err as Error).message;
    }
  }

  open(documentId: string): void {
    if (!this.candidate) {
      return;
    }
    const secure = this.documentService.createSecureUrl(this.candidate.id, documentId);
    window.open(secure.url, '_blank', 'noopener,noreferrer');
  }
}
