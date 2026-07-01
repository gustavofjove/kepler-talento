import { Component } from '@angular/core';
import { DEFAULT_LANGUAGES, DEFAULT_PROGRAMS, DEFAULT_SKILLS } from '../models/catalog.models';

@Component({
  selector: 'rrhh-catalog-management-page',
  standalone: true,
  template: `
    <section class="page">
      <div class="page-header">
        <h1>Catalogos</h1>
        <p class="muted">Listas cerradas del MVP. La persistencia final vive en Supabase.</p>
      </div>
      <div class="grid three">
        <article class="panel stack">
          <h2>Idiomas</h2>
          @for (item of languages; track item) {
            <p>
              <span class="badge">{{ item }}</span>
            </p>
          }
        </article>
        <article class="panel stack">
          <h2>Programas</h2>
          @for (item of programs; track item) {
            <p>
              <span class="badge">{{ item }}</span>
            </p>
          }
        </article>
        <article class="panel stack">
          <h2>Habilidades</h2>
          @for (item of skills; track item) {
            <p>
              <span class="badge">{{ item }}</span>
            </p>
          }
        </article>
      </div>
    </section>
  `,
})
export class CatalogManagementPageComponent {
  readonly languages = DEFAULT_LANGUAGES;
  readonly programs = DEFAULT_PROGRAMS;
  readonly skills = DEFAULT_SKILLS;
}
