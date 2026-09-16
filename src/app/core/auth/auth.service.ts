import { signal } from '../../core/state/signal';
import type { Permission, UserProfile } from '../../shared/models/auth.models';
import type { AppNavigator } from '../routing/navigator';
import type { ApiTransport } from '../http/api-transport';
import type { TokenSource } from './token-source';

export class AuthService {
  readonly profile = signal<UserProfile | null>(null);

  constructor(
    private readonly tokenSource: TokenSource,
    private readonly api: ApiTransport,
    private readonly router: AppNavigator,
  ) {}

  get isAuthenticated(): boolean {
    return this.profile() !== null;
  }

  hasPermission(permission: Permission): boolean {
    const profile = this.profile();
    return Boolean(profile?.isActive && profile.permissions.includes(permission));
  }

  async signIn(): Promise<void> {
    await this.tokenSource.signIn();
    const profile = await this.api.request<UserProfile & { roleName?: string }>('/me');
    this.profile.set({ ...profile, role: profile.roleName ?? profile.role });
  }

  async signOut(): Promise<void> {
    this.profile.set(null);
    await this.tokenSource.signOut();
    await this.router.navigateByUrl('/login');
  }

  async handleUnauthorized(): Promise<void> {
    if (this.profile() !== null) await this.signOut();
  }
}
