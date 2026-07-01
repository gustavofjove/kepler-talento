import { Injectable, signal } from '@angular/core';
import { Router } from '@angular/router';
import { DEFAULT_ROLES, Permission, UserProfile } from '../../shared/models/auth.models';
import { SupabaseClientService } from '../supabase/supabase-client.service';

const STORAGE_KEY = 'rrhh-demo-profile';

@Injectable({ providedIn: 'root' })
export class AuthService {
  readonly profile = signal<UserProfile | null>(this.restoreProfile());

  constructor(
    private readonly supabaseClient: SupabaseClientService,
    private readonly router: Router,
  ) {}

  get isAuthenticated(): boolean {
    return this.profile() !== null;
  }

  hasPermission(permission: Permission): boolean {
    const profile = this.profile();
    return Boolean(profile?.isActive && profile.permissions.includes(permission));
  }

  async signIn(email: string, password: string, role = 'rrhh_admin'): Promise<void> {
    const client = this.supabaseClient.supabase;
    if (client) {
      const { error } = await client.auth.signInWithPassword({ email, password });
      if (error) {
        throw error;
      }
    }

    const roleDefinition = DEFAULT_ROLES.find((item) => item.name === role) ?? DEFAULT_ROLES[0];
    const profile: UserProfile = {
      id: crypto.randomUUID(),
      displayName: email.split('@')[0] || 'Usuario RRHH',
      email,
      role: roleDefinition.name,
      isActive: true,
      mfaRequired: false,
      permissions: roleDefinition.permissions,
    };
    localStorage.setItem(STORAGE_KEY, JSON.stringify(profile));
    this.profile.set(profile);
  }

  async signOut(): Promise<void> {
    const client = this.supabaseClient.supabase;
    if (client) {
      await client.auth.signOut();
    }
    localStorage.removeItem(STORAGE_KEY);
    this.profile.set(null);
    await this.router.navigateByUrl('/login');
  }

  private restoreProfile(): UserProfile | null {
    const raw = localStorage.getItem(STORAGE_KEY);
    if (!raw) {
      return null;
    }

    try {
      return JSON.parse(raw) as UserProfile;
    } catch {
      localStorage.removeItem(STORAGE_KEY);
      return null;
    }
  }
}
