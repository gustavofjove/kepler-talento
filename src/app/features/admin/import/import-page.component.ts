import { Component } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ImportSummary } from './import.models';
import { ImportService } from './import.service';

@Component({
  selector: 'rrhh-import-page',
  standalone: true,
  imports: [FormsModule],
  template: `
    <section class="page">
      <div class="page-header">
        <h1>Importacion Access/CSV</h1>
        <p class="muted">Flujo de carga controlada. Access no se usa como backend operativo.</p>
      </div>
      <div class="panel grid">
        <div class="field">
          <label>CSV depurado</label>
          <input type="file" accept=".csv" (change)="select($event)" />
        </div>
        <label class="inline-check"
          ><input type="checkbox" [(ngModel)]="dryRun" /> Validar sin cargar</label
        >
        <button class="button" type="button" [disabled]="!file" (click)="process()">
          Procesar
        </button>
      </div>
      @if (summary) {
        <div class="panel stack">
          <h2>Resumen</h2>
          <p><strong>Origen:</strong> {{ summary.sourceName }}</p>
          <p><strong>Modo:</strong> {{ summary.dryRun ? 'Dry run' : 'Carga' }}</p>
          <p class="muted">La Edge Function final registrara filas cargadas y errores.</p>
        </div>
      }
    </section>
  `,
})
export class ImportPageComponent {
  file: File | null = null;
  dryRun = true;
  summary: ImportSummary | null = null;

  constructor(private readonly importService: ImportService) {}

  select(event: Event): void {
    const input = event.target as HTMLInputElement;
    this.file = input.files?.[0] ?? null;
  }

  process(): void {
    if (this.file) {
      this.summary = this.importService.validateLocalCsv(this.file, this.dryRun);
    }
  }
}
