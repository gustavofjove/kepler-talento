import { Component } from '@angular/core';
import { DEFAULT_ROLES } from '../../../shared/models/auth.models';

@Component({
  selector: 'rrhh-admin-roles-page',
  standalone: true,
  template: `
    <section class="page">
      <div class="page-header">
        <h1>Roles</h1>
        <p class="muted">Permisos granulares usados por RLS y por la interfaz.</p>
      </div>
      <div class="grid two">
        @for (role of roles; track role.name) {
          <article class="panel stack">
            <h2>{{ role.label }}</h2>
            <p class="muted">{{ role.name }}</p>
            @for (permission of role.permissions; track permission) {
              <span class="badge">{{ permission }}</span>
            }
          </article>
        }
      </div>
    </section>
  `,
})
export class AdminRolesPageComponent {
  readonly roles = DEFAULT_ROLES;
}
