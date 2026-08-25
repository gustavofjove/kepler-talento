import { signal } from '../../../core/state/signal';
import { RoleService } from '../roles/role.service';

const STORAGE_KEY = 'rrhh-admin-users';

export interface AdminUser {
  id: string;
  displayName: string;
  email: string;
  role: string;
  isActive: boolean;
  mfaRequired: boolean;
}

export class ProfileService {
  readonly users = signal<AdminUser[]>(this.restore());

  constructor(private readonly roles: RoleService) {}

  create(draft: Omit<AdminUser, 'id'>): AdminUser {
    const email = draft.email.trim().toLowerCase();
    const displayName = draft.displayName.trim();

    if (!email || !displayName) {
      throw new Error('Nombre y email son obligatorios.');
    }

    if (this.users().some((user) => user.email.toLowerCase() === email)) {
      throw new Error('Ya existe un usuario con ese email.');
    }

    if (!this.roles.roles().some((role) => role.name === draft.role)) {
      throw new Error('El rol seleccionado no existe.');
    }

    const created: AdminUser = {
      ...draft,
      id: crypto.randomUUID(),
      email,
      displayName,
    };

    this.persist([...this.users(), created]);
    return created;
  }

  updateRole(userId: string, role: string, actorEmail?: string): void {
    if (!this.roles.roles().some((item) => item.name === role)) {
      throw new Error('El rol seleccionado no existe.');
    }

    const target = this.users().find((item) => item.id === userId);
    if (!target) {
      throw new Error('Usuario no encontrado.');
    }

    if (actorEmail && target.email.toLowerCase() === actorEmail.toLowerCase()) {
      throw new Error('No puedes cambiar tu propio rol desde esta pantalla.');
    }

    if (
      target.isActive &&
      this.isAdminRole(target.role) &&
      !this.isAdminRole(role) &&
      this.activeAdminCountExcluding(target.id) === 0
    ) {
      throw new Error('Debe existir al menos un administrador activo.');
    }

    this.persist(
      this.users().map((user) =>
        user.id === userId
          ? {
              ...user,
              role,
            }
          : user,
      ),
    );
  }

  toggleActive(userId: string, actorEmail?: string): void {
    const target = this.users().find((item) => item.id === userId);
    if (!target) {
      throw new Error('Usuario no encontrado.');
    }

    if (actorEmail && target.email.toLowerCase() === actorEmail.toLowerCase()) {
      throw new Error('No puedes desactivarte a ti mismo.');
    }

    if (
      target.isActive &&
      this.isAdminRole(target.role) &&
      this.activeAdminCountExcluding(target.id) === 0
    ) {
      throw new Error('Debe existir al menos un administrador activo.');
    }

    this.persist(
      this.users().map((user) =>
        user.id === userId
          ? {
              ...user,
              isActive: !user.isActive,
            }
          : user,
      ),
    );
  }

  toggleMfa(userId: string): void {
    this.persist(
      this.users().map((user) =>
        user.id === userId
          ? {
              ...user,
              mfaRequired: !user.mfaRequired,
            }
          : user,
      ),
    );
  }

  remove(userId: string, actorEmail?: string): void {
    const target = this.users().find((item) => item.id === userId);
    if (!target) {
      return;
    }

    if (actorEmail && target.email.toLowerCase() === actorEmail.toLowerCase()) {
      throw new Error('No puedes eliminar tu propio usuario.');
    }

    if (
      target.isActive &&
      this.isAdminRole(target.role) &&
      this.activeAdminCountExcluding(target.id) === 0
    ) {
      throw new Error('Debe existir al menos un administrador activo.');
    }

    this.persist(this.users().filter((user) => user.id !== userId));
  }

  private isAdminRole(roleName: string): boolean {
    const permissions = this.roles.permissionsForRole(roleName);
    return permissions.includes('manage_users') || permissions.includes('manage_roles');
  }

  private activeAdminCountExcluding(userId: string): number {
    return this.users().filter(
      (user) => user.id !== userId && user.isActive && this.isAdminRole(user.role),
    ).length;
  }

  private persist(users: AdminUser[]): void {
    localStorage.setItem(STORAGE_KEY, JSON.stringify(users));
    this.users.set(users);
  }

  private restore(): AdminUser[] {
    const raw = localStorage.getItem(STORAGE_KEY);
    if (raw) {
      try {
        const parsed = JSON.parse(raw) as AdminUser[];
        if (Array.isArray(parsed)) {
          return parsed;
        }
      } catch {
        localStorage.removeItem(STORAGE_KEY);
      }
    }

    return [
      {
        id: crypto.randomUUID(),
        displayName: 'RRHH Admin',
        email: 'rrhh.admin@example.com',
        role: 'rrhh_admin',
        isActive: true,
        mfaRequired: false,
      },
    ];
  }
}
