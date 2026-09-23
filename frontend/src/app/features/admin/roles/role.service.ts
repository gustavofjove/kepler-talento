import type { ApiTransport } from '../../../core/http/api-transport';
import { signal } from '../../../core/state/signal';
import { ALL_PERMISSIONS, type Permission } from '../../../shared/models/auth.models';

export interface AdminRole {
  id: string;
  name: string;
  label: string;
  isSystem: boolean;
  permissions: Permission[];
  isActive: boolean;
  createdAt: string;
  updatedAt: string;
  version: number;
}

export class RoleService {
  readonly roles = signal<AdminRole[]>([]);
  readonly allPermissions = ALL_PERMISSIONS;
  constructor(private readonly api: ApiTransport) {}

  async load(): Promise<AdminRole[]> {
    const roles = await this.api.request<AdminRole[]>('/admin/roles');
    this.roles.set(roles);
    return roles;
  }

  async create(
    name: string,
    label: string,
    permissions: Permission[] = ['candidates.read'],
  ): Promise<AdminRole> {
    const created = await this.api.request<AdminRole>('/admin/roles', {
      method: 'POST',
      body: JSON.stringify({ name, label, permissions }),
    });
    this.roles.set([...this.roles(), created]);
    return created;
  }

  async updateLabel(role: AdminRole, label: string): Promise<AdminRole> {
    return this.replace(
      await this.api.request<AdminRole>(`/admin/roles/${role.id}`, {
        method: 'PUT',
        body: JSON.stringify({ label, version: role.version }),
      }),
    );
  }

  async setPermissions(role: AdminRole, permissions: Permission[]): Promise<AdminRole> {
    return this.replace(
      await this.api.request<AdminRole>(`/admin/roles/${role.id}/permissions`, {
        method: 'PUT',
        body: JSON.stringify({ permissions, version: role.version }),
      }),
    );
  }

  async setActive(role: AdminRole, isActive: boolean): Promise<AdminRole> {
    return this.replace(
      await this.api.request<AdminRole>(`/admin/roles/${role.id}/active`, {
        method: 'PUT',
        body: JSON.stringify({ isActive, version: role.version }),
      }),
    );
  }

  private replace(updated: AdminRole): AdminRole {
    this.roles.set(this.roles().map((role) => (role.id === updated.id ? updated : role)));
    return updated;
  }
}
