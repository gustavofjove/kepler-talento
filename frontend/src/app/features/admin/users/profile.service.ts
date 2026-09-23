import type { ApiTransport } from '../../../core/http/api-transport';
import { signal } from '../../../core/state/signal';

export interface AdminUser {
  id: string;
  displayName: string;
  email: string;
  roleName: string;
  isActive: boolean;
  lastSignInAt: string | null;
  createdAt: string;
  updatedAt: string;
  version: number;
}

export interface CreateAdminUser {
  displayName: string;
  email: string;
  roleName: string;
}

interface AdminUserPage {
  items: AdminUser[];
  page: number;
  pageSize: number;
  totalCount: number;
}

export class ProfileService {
  readonly users = signal<AdminUser[]>([]);
  constructor(private readonly api: ApiTransport) {}

  async load(): Promise<AdminUser[]> {
    const result = await this.api.request<AdminUserPage>(
      '/admin/users?includeInactive=true&page=1&pageSize=100',
    );
    this.users.set(result.items);
    return result.items;
  }

  async create(draft: CreateAdminUser): Promise<AdminUser> {
    const created = await this.api.request<AdminUser>('/admin/users', {
      method: 'POST',
      body: JSON.stringify(draft),
    });
    this.users.set([...this.users(), created]);
    return created;
  }

  async update(user: AdminUser, displayName: string): Promise<AdminUser> {
    return this.replace(
      await this.api.request<AdminUser>(`/admin/users/${user.id}`, {
        method: 'PUT',
        body: JSON.stringify({ displayName, version: user.version }),
      }),
    );
  }

  async setRole(user: AdminUser, roleName: string): Promise<AdminUser> {
    return this.replace(
      await this.api.request<AdminUser>(`/admin/users/${user.id}/role`, {
        method: 'PUT',
        body: JSON.stringify({ roleName, version: user.version }),
      }),
    );
  }

  async setActive(user: AdminUser, isActive: boolean): Promise<AdminUser> {
    return this.replace(
      await this.api.request<AdminUser>(`/admin/users/${user.id}/active`, {
        method: 'PUT',
        body: JSON.stringify({ isActive, version: user.version }),
      }),
    );
  }

  private replace(updated: AdminUser): AdminUser {
    this.users.set(this.users().map((user) => (user.id === updated.id ? updated : user)));
    return updated;
  }
}
