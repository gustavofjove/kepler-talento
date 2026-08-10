import { Injectable, signal } from '@angular/core';
import {
  ALL_PERMISSIONS,
  DEFAULT_ROLES,
  Permission,
  RoleDefinition,
} from '../../../shared/models/auth.models';

const STORAGE_KEY = 'rrhh-admin-roles';

@Injectable({ providedIn: 'root' })
export class RoleService {
  readonly roles = signal<RoleDefinition[]>(this.restore());
  readonly allPermissions = ALL_PERMISSIONS;

  create(name: string, label: string): RoleDefinition {
    const normalizedName = name.trim().toLowerCase();
    const normalizedLabel = label.trim();

    if (!normalizedName || !normalizedLabel) {
      throw new Error('Nombre interno y etiqueta son obligatorios.');
    }

    if (!/^[a-z0-9_]+$/.test(normalizedName)) {
      throw new Error('El nombre interno solo admite minusculas, numeros y guion bajo.');
    }

    if (this.roles().some((role) => role.name === normalizedName)) {
      throw new Error('Ya existe un rol con ese nombre interno.');
    }

    const created: RoleDefinition = {
      name: normalizedName,
      label: normalizedLabel,
      permissions: ['view_candidates'],
      isSystem: false,
    };

    this.persist([...this.roles(), created]);
    return created;
  }

  updateLabel(name: string, label: string): void {
    const normalizedLabel = label.trim();
    if (!normalizedLabel) {
      throw new Error('La etiqueta del rol no puede quedar vacia.');
    }

    const role = this.roles().find((item) => item.name === name);
    if (!role) {
      throw new Error('El rol no existe.');
    }
    if (role.isSystem) {
      throw new Error('No se puede editar un rol de sistema.');
    }

    this.persist(
      this.roles().map((role) =>
        role.name === name
          ? {
              ...role,
              label: normalizedLabel,
            }
          : role,
      ),
    );
  }

  setPermission(name: string, permission: Permission, enabled: boolean): void {
    const role = this.roles().find((item) => item.name === name);
    if (!role) {
      throw new Error('El rol no existe.');
    }
    if (role.isSystem) {
      throw new Error('No se pueden modificar permisos de un rol de sistema.');
    }

    this.persist(
      this.roles().map((role) => {
        if (role.name !== name) {
          return role;
        }

        const nextPermissions = enabled
          ? Array.from(new Set([...role.permissions, permission]))
          : role.permissions.filter((item) => item !== permission);

        if (!nextPermissions.length) {
          throw new Error('Cada rol debe tener al menos un permiso.');
        }

        return {
          ...role,
          permissions: nextPermissions,
        };
      }),
    );
  }

  remove(name: string): void {
    const role = this.roles().find((item) => item.name === name);
    if (!role) {
      return;
    }

    if (role.isSystem) {
      throw new Error('No se puede eliminar un rol de sistema.');
    }

    this.persist(this.roles().filter((item) => item.name !== name));
  }

  permissionsForRole(name: string): Permission[] {
    const role = this.roles().find((item) => item.name === name);
    return role?.permissions ?? [];
  }

  private persist(roles: RoleDefinition[]): void {
    localStorage.setItem(STORAGE_KEY, JSON.stringify(roles));
    this.roles.set(roles);
  }

  private restore(): RoleDefinition[] {
    const raw = localStorage.getItem(STORAGE_KEY);
    if (raw) {
      try {
        const parsed = JSON.parse(raw) as RoleDefinition[];
        if (Array.isArray(parsed) && parsed.length) {
          return parsed.map((role) => ({
            ...role,
            isSystem: role.isSystem ?? DEFAULT_ROLES.some((base) => base.name === role.name),
          }));
        }
      } catch {
        localStorage.removeItem(STORAGE_KEY);
      }
    }
    return DEFAULT_ROLES.map((role) => ({ ...role }));
  }
}
