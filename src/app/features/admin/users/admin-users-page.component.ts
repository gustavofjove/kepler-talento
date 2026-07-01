import { Component } from '@angular/core';
import { DEFAULT_ROLES } from '../../../shared/models/auth.models';

@Component({
  selector: 'rrhh-admin-users-page',
  standalone: true,
  template: `
    <section class="page">
      <div class="page-header">
        <h1>Usuarios</h1>
        <p class="muted">Gestion funcional preparada para Edge Functions de administracion.</p>
      </div>
      <div class="panel stack">
        <p>Roles disponibles para alta de usuarios:</p>
        @for (role of roles; track role.name) {
          <p>
            <span class="badge">{{ role.name }}</span> {{ role.label }}
          </p>
        }
      </div>
    </section>
  `,
})
export class AdminUsersPageComponent {
  readonly roles = DEFAULT_ROLES;
}
