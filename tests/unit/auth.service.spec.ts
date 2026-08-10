import { AuthService } from '../../src/app/core/auth/auth.service';

describe('AuthService', () => {
  const createService = () => {
    const router = {
      navigateByUrl: jest.fn().mockResolvedValue(true),
    };
    const supabaseClient = {
      supabase: null,
    };
    const service = new AuthService(supabaseClient as any, router as any);
    return { service, router };
  };

  beforeEach(() => {
    localStorage.clear();
  });

  it('returns unauthenticated state when no profile is stored', () => {
    const { service } = createService();

    expect(service.isAuthenticated).toBe(false);
    expect(service.profile()).toBeNull();
  });

  it('does not grant permissions to inactive profiles', () => {
    const { service } = createService();
    service.profile.set({
      id: 'u-1',
      displayName: 'Test',
      email: 'inactive@example.com',
      role: 'rrhh_admin',
      isActive: false,
      mfaRequired: false,
      permissions: ['view_candidates', 'edit_candidates'],
    });

    expect(service.hasPermission('view_candidates')).toBe(false);
  });

  it('keeps readonly user without create/edit permissions', async () => {
    const { service } = createService();

    await service.signIn('readonly@example.com', 'local-demo', 'readonly');

    expect(service.hasPermission('view_candidates')).toBe(true);
    expect(service.hasPermission('create_candidates')).toBe(false);
    expect(service.hasPermission('edit_candidates')).toBe(false);
  });

  it('signs out and clears local profile', async () => {
    const { service, router } = createService();
    await service.signIn('rrhh.admin@example.com', 'local-demo', 'rrhh_admin');

    await service.signOut();

    expect(service.profile()).toBeNull();
    expect(router.navigateByUrl).toHaveBeenCalledWith('/login');
  });

  it('restores stored profile preserving MFA readiness flag', () => {
    localStorage.setItem(
      'rrhh-demo-profile',
      JSON.stringify({
        id: 'u-mfa',
        displayName: 'MFA User',
        email: 'mfa@example.com',
        role: 'rrhh_admin',
        isActive: true,
        mfaRequired: true,
        permissions: ['view_candidates'],
      }),
    );

    const { service } = createService();
    expect(service.isAuthenticated).toBe(true);
    expect(service.profile()?.mfaRequired).toBe(true);
  });
});
