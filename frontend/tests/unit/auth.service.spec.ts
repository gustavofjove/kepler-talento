import { AuthService } from '../../src/app/core/auth/auth.service';

describe('AuthService', () => {
  const createService = () => {
    const router = { navigateByUrl: vi.fn().mockResolvedValue(true) };
    const tokenSource = {
      signIn: vi.fn().mockResolvedValue('token'),
      getToken: vi.fn().mockResolvedValue('token'),
      signOut: vi.fn().mockResolvedValue(undefined),
    };
    const api = {
      request: vi.fn().mockResolvedValue({
        id: 'u-1',
        displayName: 'Test',
        email: 'test@example.com',
        roleName: 'readonly',
        roleLabel: 'Solo lectura',
        isActive: true,
        permissions: ['candidates.read'],
      }),
    };
    const service = new AuthService(
      tokenSource,
      api as unknown as ConstructorParameters<typeof AuthService>[1],
      router,
    );
    return { service, router, tokenSource, api };
  };

  it('starts without a browser-restored profile', () => {
    expect(createService().service.profile()).toBeNull();
  });

  it('sign-in acquires a token and fills the profile from api me', async () => {
    const { service, tokenSource, api } = createService();
    await service.signIn();
    expect(tokenSource.signIn).toHaveBeenCalledOnce();
    expect(api.request).toHaveBeenCalledWith('/me');
    expect(service.profile()?.role).toBe('readonly');
    expect(service.hasPermission('candidates.read')).toBe(true);
  });

  it('signs out of both sessions and redirects', async () => {
    const { service, tokenSource, router } = createService();
    await service.signIn();
    await service.signOut();
    expect(service.profile()).toBeNull();
    expect(tokenSource.signOut).toHaveBeenCalledOnce();
    expect(router.navigateByUrl).toHaveBeenCalledWith('/login');
  });

  it('a 401 notification clears the active session', async () => {
    const { service, router } = createService();
    await service.signIn();
    await service.handleUnauthorized();
    expect(service.profile()).toBeNull();
    expect(router.navigateByUrl).toHaveBeenCalledWith('/login');
  });
});
