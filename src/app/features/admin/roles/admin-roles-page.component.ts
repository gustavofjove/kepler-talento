import { Component } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ToastService } from '../../../core/services/toast.service';
import { Permission, RoleDefinition } from '../../../shared/models/auth.models';
import { ProfileService } from '../users/profile.service';
import { RoleService } from './role.service';
import { ConfirmDialogService } from '../../../shared/components/confirm-dialog.service';

@Component({
  selector: 'rrhh-admin-roles-page',
  standalone: true,
  imports: [FormsModule],
  template: `
    <section class="page">
      <div class="page-header">
        <h1>Roles</h1>
        <p class="muted">Gestiona roles y permisos funcionales para operar la aplicacion.</p>
      </div>

      <form class="panel grid two" (ngSubmit)="createRole()">
        <div class="field">
          <label>Nombre interno</label>
          <input
            name="name"
            [(ngModel)]="newRoleName"
            placeholder="ej: recruiter_junior"
            required
          />
        </div>
        <div class="field">
          <label>Etiqueta visible</label>
          <input
            name="label"
            [(ngModel)]="newRoleLabel"
            placeholder="ej: Recruiter Junior"
            required
          />
        </div>
        <div class="form-actions" style="grid-column: 1 / -1;">
          <button class="button" type="submit">Crear rol</button>
        </div>
      </form>

      <div class="grid two">
        @for (role of roles.roles(); track role.name) {
          <article class="panel stack">
            <div class="toolbar">
              @if (editingRole === role.name) {
                <input name="editLabel" [(ngModel)]="editLabel" />
                <button class="button" type="button" (click)="saveLabel(role)">Guardar</button>
              } @else {
                <h2>{{ role.label }}</h2>
                <button class="button secondary" type="button" (click)="startEditLabel(role)">
                  Editar nombre
                </button>
              }
            </div>
            <p class="muted">
              {{ role.name }} · {{ role.isSystem ? 'Rol de sistema' : 'Rol personalizado' }}
            </p>
            <div class="grid">
              @for (permission of roles.allPermissions; track permission) {
                <label class="inline-check">
                  <input
                    type="checkbox"
                    [checked]="hasPermission(role, permission)"
                    (change)="togglePermission(role, permission, $any($event.target).checked)"
                  />
                  {{ permission }}
                </label>
              }
            </div>
            <div class="form-actions">
              <button
                class="button danger"
                type="button"
                [disabled]="role.isSystem || isRoleInUse(role.name)"
                (click)="remove(role)"
              >
                Eliminar rol
              </button>
            </div>
          </article>
        }
      </div>
    </section>
  `,
})
export class AdminRolesPageComponent {
  newRoleName = '';
  newRoleLabel = '';
  editingRole = '';
  editLabel = '';

  constructor(
    readonly roles: RoleService,
    private readonly users: ProfileService,
    private readonly toast: ToastService,
    private readonly confirmDialog: ConfirmDialogService,
  ) {}

  createRole(): void {
    try {
      this.roles.create(this.newRoleName, this.newRoleLabel);
      this.newRoleName = '';
      this.newRoleLabel = '';
      this.toast.show('Rol creado.', 'success');
    } catch (error) {
      this.toast.show(error instanceof Error ? error.message : 'No se pudo crear el rol.', 'error');
    }
  }

  startEditLabel(role: RoleDefinition): void {
    this.editingRole = role.name;
    this.editLabel = role.label;
  }

  saveLabel(role: RoleDefinition): void {
    try {
      this.roles.updateLabel(role.name, this.editLabel);
      this.editingRole = '';
      this.editLabel = '';
      this.toast.show('Rol actualizado.', 'success');
    } catch (error) {
      this.toast.show(error instanceof Error ? error.message : 'No se pudo actualizar.', 'error');
    }
  }

  hasPermission(role: RoleDefinition, permission: Permission): boolean {
    return role.permissions.includes(permission);
  }

  togglePermission(role: RoleDefinition, permission: Permission, enabled: boolean): void {
    try {
      this.roles.setPermission(role.name, permission, enabled);
    } catch (error) {
      this.toast.show(
        error instanceof Error ? error.message : 'No se pudo actualizar permisos.',
        'error',
      );
    }
  }

  isRoleInUse(roleName: string): boolean {
    return this.users.users().some((user) => user.role === roleName);
  }

  async remove(role: RoleDefinition): Promise<void> {
    const confirmed = await this.confirmDialog.confirm({
      title: 'Eliminar rol',
      message: `Se eliminara el rol "${role.label}".`,
      confirmText: 'Eliminar rol',
      cancelText: 'Cancelar',
      danger: true,
    });
    if (!confirmed) {
      return;
    }

    try {
      this.roles.remove(role.name);
      this.toast.show('Rol eliminado.', 'success');
    } catch (error) {
      this.toast.show(
        error instanceof Error ? error.message : 'No se pudo eliminar el rol.',
        'error',
      );
    }
  }
}
