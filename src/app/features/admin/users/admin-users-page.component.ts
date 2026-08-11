import { Component } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { AuthService } from '../../../core/auth/auth.service';
import { ToastService } from '../../../core/services/toast.service';
import { ProfileService } from './profile.service';
import { RoleService } from '../roles/role.service';
import { ConfirmDialogService } from '../../../shared/components/confirm-dialog.service';

@Component({
  selector: 'rrhh-admin-users-page',
  standalone: true,
  imports: [FormsModule],
  template: `
    <section class="page">
      <div class="page-header">
        <h1>Usuarios</h1>
        <p class="muted">Alta y mantenimiento de usuarios internos por rol.</p>
      </div>

      <form class="panel grid two" (ngSubmit)="createUser()">
        <div class="field">
          <label>Nombre</label>
          <input name="displayName" [(ngModel)]="draft.displayName" required />
        </div>
        <div class="field">
          <label>Email</label>
          <input name="email" type="email" [(ngModel)]="draft.email" required />
        </div>
        <div class="field">
          <label>Rol</label>
          <select name="role" [(ngModel)]="draft.role" required>
            @for (role of roleService.roles(); track role.name) {
              <option [value]="role.name">{{ role.label }}</option>
            }
          </select>
        </div>
        <div class="field">
          <label class="inline-check">
            <input name="mfa" type="checkbox" [(ngModel)]="draft.mfaRequired" />
            Requerir MFA
          </label>
        </div>
        <div class="form-actions" style="grid-column: 1 / -1;">
          <button class="button" type="submit">Crear usuario</button>
        </div>
      </form>

      <div class="panel table-wrap">
        @if (!users.users().length) {
          <p class="empty-state">
            No hay usuarios cargados. Crea el primero para iniciar la operación.
          </p>
        }
        <table>
          <thead>
            <tr>
              <th>Nombre</th>
              <th>Email</th>
              <th>Rol</th>
              <th>Estado</th>
              <th>MFA</th>
              <th>Acciones</th>
            </tr>
          </thead>
          <tbody>
            @for (user of users.users(); track user.id) {
              <tr>
                <td>{{ user.displayName }}</td>
                <td>{{ user.email }}</td>
                <td>
                  <select
                    [ngModel]="user.role"
                    (ngModelChange)="changeRole(user.id, $event)"
                    [name]="'role_' + user.id"
                  >
                    @for (role of roleService.roles(); track role.name) {
                      <option [value]="role.name">{{ role.label }}</option>
                    }
                  </select>
                </td>
                <td>
                  <span class="badge">{{ user.isActive ? 'Activo' : 'Inactivo' }}</span>
                </td>
                <td>
                  <span class="badge">{{ user.mfaRequired ? 'Obligatoria' : 'Opcional' }}</span>
                </td>
                <td>
                  <div class="form-actions">
                    <button class="button secondary" type="button" (click)="toggleActive(user.id)">
                      {{ user.isActive ? 'Desactivar' : 'Activar' }}
                    </button>
                    <button class="button secondary" type="button" (click)="toggleMfa(user.id)">
                      Cambiar MFA
                    </button>
                    <button class="button danger" type="button" (click)="remove(user.id)">
                      Eliminar
                    </button>
                  </div>
                </td>
              </tr>
            }
          </tbody>
        </table>
      </div>
    </section>
  `,
})
export class AdminUsersPageComponent {
  draft = {
    displayName: '',
    email: '',
    role: 'rrhh_user',
    isActive: true,
    mfaRequired: false,
  };

  constructor(
    private readonly auth: AuthService,
    readonly users: ProfileService,
    readonly roleService: RoleService,
    private readonly toast: ToastService,
    private readonly confirmDialog: ConfirmDialogService,
  ) {}

  createUser(): void {
    try {
      this.users.create(this.draft);
      this.draft = {
        displayName: '',
        email: '',
        role: this.roleService.roles()[0]?.name || 'rrhh_user',
        isActive: true,
        mfaRequired: false,
      };
      this.toast.show('Usuario creado.', 'success');
    } catch (error) {
      this.toast.show(
        error instanceof Error ? error.message : 'No se pudo crear el usuario.',
        'error',
      );
    }
  }

  changeRole(userId: string, role: string): void {
    try {
      this.users.updateRole(userId, role, this.auth.profile()?.email);
      this.toast.show('Rol actualizado.', 'success');
    } catch (error) {
      this.toast.show(
        error instanceof Error ? error.message : 'No se pudo actualizar el rol.',
        'error',
      );
    }
  }

  toggleActive(userId: string): void {
    try {
      this.users.toggleActive(userId, this.auth.profile()?.email);
      this.toast.show('Estado actualizado.', 'success');
    } catch (error) {
      this.toast.show(
        error instanceof Error ? error.message : 'No se pudo actualizar el estado.',
        'error',
      );
    }
  }

  toggleMfa(userId: string): void {
    try {
      this.users.toggleMfa(userId);
      this.toast.show('MFA actualizado.', 'success');
    } catch (error) {
      this.toast.show(
        error instanceof Error ? error.message : 'No se pudo actualizar MFA.',
        'error',
      );
    }
  }

  async remove(userId: string): Promise<void> {
    const confirmed = await this.confirmDialog.confirm({
      title: 'Eliminar usuario',
      message: 'Esta acción retirará el usuario de la operación actual.',
      confirmText: 'Eliminar usuario',
      cancelText: 'Cancelar',
      danger: true,
    });
    if (!confirmed) {
      return;
    }
    try {
      this.users.remove(userId, this.auth.profile()?.email);
      this.toast.show('Usuario eliminado.', 'success');
    } catch (error) {
      this.toast.show(
        error instanceof Error ? error.message : 'No se pudo eliminar el usuario.',
        'error',
      );
    }
  }
}
